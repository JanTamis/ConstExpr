using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.BuildIn;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Refactorers;
using ConstExpr.SourceGenerator.Rewriters;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using SGF;
using SourceGen.Utilities.Helpers;

[assembly: InternalsVisibleTo("ConstExpr.Tests")]

namespace ConstExpr.SourceGenerator;

[IncrementalGenerator]
public class ConstExprSourceGenerator() : IncrementalGenerator("ConstExpr")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		AppDomain.CurrentDomain.TypeResolve += (sender, args) =>
		{
			return null;
		};

		// Use WithComparer for incremental generation caching
		// This prevents reprocessing invocations that haven't changed
		var invocations = context.SyntaxProvider
			.CreateSyntaxProvider(
				(node, token) => !token.IsCancellationRequested && node is InvocationExpressionSyntax,
				GenerateSource)
			.Where(result => result != null)
			.WithComparer(InvocationModelEqualityComparer.Instance);

		// The enable switch lives in GlobalOptions (it arrives as a build_property), but the
		// formatting keys live in per-file sections such as [*.cs] and are only reachable through
		// GetOptions(tree). The first syntax tree stands in for the project: the generated files all
		// land in one namespace, so resolving the style per invocation would only make a single
		// output file internally inconsistent when a subfolder overrides the style.
		var settings = context
			.AnalyzerConfigOptionsProvider
			.Combine(context.CompilationProvider)
			.Select((pair, _) => (
				Enabled: pair.Left.GlobalOptions.TryGetValue("build_property.UseConstExpr", out var enableSwitch)
				         && enableSwitch.Equals("true", StringComparison.Ordinal),
				Formatting: FormattingOptions.Read(pair.Left, pair.Right.SyntaxTrees.FirstOrDefault())));

		context.RegisterSourceOutput(invocations.Collect().Combine(context.CompilationProvider).Combine(settings), (spc, modelAndCompilation) =>
		{
			if (modelAndCompilation.Right.Enabled)
			{
				var formatting = modelAndCompilation.Right.Formatting;

				// Create MetadataLoader once for all invocations
				var loader = MetadataLoader.GetLoader(modelAndCompilation.Left.Right);
				var compilation = modelAndCompilation.Left.Right;

				// Create CallGraphAnalyzer once for all invocations to enable caching
				var callGraphAnalyzer = new CallGraphAnalyzer(compilation);

				// Thread-safe cache for semantic models (ConcurrentDictionary for parallel access)
				var semanticModelCache = new ConcurrentDictionary<SyntaxTree, SemanticModel>();

				// Thread-safe cache for Roslyn API results to avoid repeated expensive calls
				var roslynApiCache = new RoslynApiCache();

				// Filter out invocations whose containing method is never called
				// Do this filtering BEFORE parallel processing to reduce work
				var relevantInvocations = modelAndCompilation.Left.Left
					.Where(model => model is { AttributeData: not null, MethodSymbol: not null, Invocation: not null })
					.Where(model => callGraphAnalyzer.IsContainingMethodInvoked(model.Invocation, spc.CancellationToken))
					.ToList(); // Materialize to avoid multiple enumeration

				// Process all invocations in parallel with the shared loader
				var processedModels = new ConcurrentBag<InvocationModel>();

				// Parallel processing of invocations for better performance
				Parallel.ForEach(
					relevantInvocations,
					new ParallelOptions
					{
						CancellationToken = spc.CancellationToken,
						// Use default parallelism (-1) to let TPL decide optimal thread count
						MaxDegreeOfParallelism = -1
					},
					model =>
					{
						try
						{
							var attribute = model.AttributeData;

							// Thread-safe semantic model caching
							var semanticModel = semanticModelCache.GetOrAdd(
								model.Invocation.SyntaxTree,
								tree => compilation.GetSemanticModel(tree));

							var processedModel = GenerateExpression(semanticModel, loader, model.Invocation, model.MethodSymbol, attribute, roslynApiCache, formatting, spc.CancellationToken);

							if (processedModel != null)
							{
								processedModels.Add(processedModel);
							}
						}
						catch (Exception ex)
						{
							// spc.ReportDiagnostic(Diagnostic.Create(new BodyAnalyzer().SupportedDiagnostics[0], model.Invocation.GetLocation(), ex.Message));

							Logger.Warning(ex, $"Error processing invocation {model.Invocation}: {ex.Message}");
						}
					});

				// Parallel processing of method group code generation
				var methodGroups = processedModels.GroupBy(m => m.OriginalMethod, SyntaxNodeComparer.Get<MethodDeclarationSyntax?>());

				if (modelAndCompilation.Left.Right.GetTypeByMetadataName("System.Runtime.CompilerServices.InterceptsLocationAttribute") is null
				    && methodGroups.Any())
				{
					spc.AddSource("InterceptsLocationAttribute.g.cs", Finish(formatting, RenderTemplate(
						compilation,
						formatting,
						"""
						// <auto-generated />
						using System;
						""",
						"System.Runtime.CompilerServices",
						"""
						[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
						internal sealed class InterceptsLocationAttribute : Attribute
						{
							public InterceptsLocationAttribute(int version, string data)
							{
							}
						}
						""")));
				}

				var usesVectorAll = processedModels.Any(m => InvokesVectorOperationsMethod(m.Method, "All"));
				var usesVectorAny = processedModels.Any(m => InvokesVectorOperationsMethod(m.Method, "Any"));
				var usesVectorOperations = usesVectorAll || usesVectorAny;
				var usesInferfaces = processedModels.Any(m => m.Usings?.Contains("ConstantExpression.Interfaces") == true);

				if (usesInferfaces)
				{
					spc.AddSource("IOperator.g.cs", Finish(formatting, RenderTemplate(
						compilation,
						formatting,
						"""
						using System;
						using System.ComponentModel;
						using System.Numerics;
						""",
						"ConstantExpression.Interfaces",
						"""
						[EditorBrowsable(EditorBrowsableState.Never)]
						internal interface IOperator<T>
						{
							static abstract bool IsVectorizable { get; }
							static abstract bool Invoke(T item);
							static abstract Vector<T> Invoke(Vector<T> vector);
						}
						""")));
				}

				if (usesVectorOperations)
				{
					const string allMethod = """
						public static bool All<T, TOperator>(ReadOnlySpan<T> data)
							where TOperator : struct, IOperator<T>
						{
							var i = 0;
							var length = data.Length;
							var count = Vector<T>.Count;

							ref var reference = ref MemoryMarshal.GetReference(data);

							if (Vector.IsHardwareAccelerated && TOperator.IsVectorizable && (uint)length >= (uint)count)
							{
								do
								{
									var vector = Vector.LoadUnsafe(ref reference, (nuint)i);
									var mask = TOperator.Invoke(vector);

									if (Vector.EqualsAny(mask, Vector<T>.Zero))
										return false;

									i += count;
								}
								while ((uint)i < (uint)(length - count));

								if ((uint)i < (uint)length)
								{
									var remainderVector = Vector.LoadUnsafe(ref reference, (nuint)(data.Length - count));
									var remainderMask = TOperator.Invoke(remainderVector);

									if (Vector.EqualsAny(remainderMask, Vector<T>.Zero))
										return false;
								}
							}

							for (; (uint)i < (uint)length; i++)
							{
								var item = Unsafe.Add(ref reference, i);

								if (!TOperator.Invoke(item))
									return false;
							}

							return true;
						}
						""";

					const string anyMethod = """
						public static bool Any<T, TOperator>(ReadOnlySpan<T> data)
							where TOperator : struct, IOperator<T>
						{
							var i = 0;
							var length = data.Length;
							var count = Vector<T>.Count;

							ref var reference = ref MemoryMarshal.GetReference(data);

							if (Vector.IsHardwareAccelerated && TOperator.IsVectorizable && (uint)length >= (uint)count)
							{
								do
								{
									var vector = Vector.LoadUnsafe(ref reference, (nuint)i);
									var mask = TOperator.Invoke(vector);

									if (Vector.EqualsAny(mask, Vector<T>.AllBitsSet))
										return true;

									i += count;
								}
								while ((uint)i < (uint)(length - count));

								if ((uint)i < (uint)length)
								{
									var remainderVector = Vector.LoadUnsafe(ref reference, (nuint)(data.Length - count));
									var remainderMask = TOperator.Invoke(remainderVector);

									return Vector.EqualsAny(remainderMask, Vector<T>.AllBitsSet);
								}
							}

							for (; (uint)i < (uint)length; i++)
							{
								var item  = Unsafe.Add(ref reference, i);

								if (TOperator.Invoke(item))
									return true;
							}

							return false;
						}
						""";

					var vectorOperationsMembers = String.Join("\n\n", new[]
					{
						usesVectorAll ? allMethod : null,
						usesVectorAny ? anyMethod : null
					}.Where(m => m != null));

					spc.AddSource("VectorOperations.g.cs", Finish(formatting, RenderTemplate(
						compilation,
						formatting,
						"""
						using System;
						using System.ComponentModel;
						using System.Numerics;
						using System.Runtime.CompilerServices;
						using System.Runtime.InteropServices;
						using ConstantExpression.Interfaces;
						""",
						"ConstantExpression.Operations",
						$$"""
						[EditorBrowsable(EditorBrowsableState.Never)]
						internal static class VectorOperations
						{
						{{Indent(vectorOperationsMembers, 1)}}
						}
						""")));
				}

				// Generate source code in parallel but collect results first
				// spc.AddSource is NOT thread-safe, so we must add sources sequentially
				var generatedSources = new ConcurrentBag<(string FileName, string Source)>();

				Parallel.ForEach(
					methodGroups,
					new ParallelOptions
					{
						CancellationToken = spc.CancellationToken,
						// Use default parallelism (-1) to let TPL decide optimal thread count
						MaxDegreeOfParallelism = -1
					},
					methodGroup =>
					{
						try
						{
							var result = GenerateMethodSource(compilation, methodGroup, loader, formatting);

							if (result != null)
							{
								generatedSources.Add(result.Value);
							}
						}
						catch (Exception ex)
						{
							Logger.Error(ex, $"Error generating implementations for {methodGroup.Key.Identifier}: {ex.Message}");
						}
					});

				// Add all generated sources sequentially (thread-safe)
				foreach (var (fileName, source) in generatedSources)
				{
					spc.AddSource(fileName, Finish(formatting, source));
				}

				ReportExceptions(spc, processedModels);

				// Clear caches to free memory after processing
				roslynApiCache.Clear();
				callGraphAnalyzer.ClearCache();
			}
		});
	}

	/// <summary>
	///   Turns a finished file into the <see cref="SourceText" /> handed to the compiler, applying
	///   the options that only make sense on the complete text.
	/// </summary>
	/// <remarks>
	///   Only the <see cref="SourceText" /> overload of <c>AddSource</c> lets the encoding be
	///   chosen; passing a plain string always yields UTF-8 with a byte order mark, which is what
	///   <c>charset = utf-8</c> asks us not to do.
	/// </remarks>
	private static SourceText Finish(FormattingOptions formatting, string source)
	{
		if (formatting.IndentLabels == LabelIndentation.FlushLeft)
		{
			source = FlushLabelsLeft(source);
		}

		if (formatting.TrimTrailingWhitespace)
		{
			source = TrimTrailingWhitespace(source, formatting);
		}

		return SourceText.From(source, formatting.EmitByteOrderMark
			? Encoding.UTF8
			: new UTF8Encoding(false));
	}

	/// <summary>
	///   Moves every <c>goto</c> label to column 0 (<c>csharp_indent_labels = flush_left</c>).
	/// </summary>
	/// <remarks>
	///   Applied to the finished file rather than in <see cref="Rewriters.IndentationRewriter" />
	///   because column 0 is a position in the file, and a member is rendered before it is known how
	///   deeply it will be nested. The text is re-parsed rather than pattern-matched: a named
	///   argument at the start of a line looks exactly like a label, and only the parser can tell
	///   them apart.
	/// </remarks>
	private static string FlushLabelsLeft(string source)
	{
		var unit = ParseCompilationUnit(source);

		var labels = unit
			.DescendantNodes()
			.OfType<LabeledStatementSyntax>()
			.Select(labeled => labeled.Identifier)
			.Where(identifier => identifier.LeadingTrivia.Any(SyntaxKind.WhitespaceTrivia))
			.ToList();

		if (labels.Count == 0)
		{
			return source;
		}

		return unit
			.ReplaceTokens(labels, (original, _) => original.WithLeadingTrivia(
				original.LeadingTrivia.Where(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia))))
			.ToFullString();
	}

	/// <summary>
	///   Removes trailing spaces and tabs from every line (<c>trim_trailing_whitespace</c>).
	/// </summary>
	/// <remarks>
	///   Lines inside a multi-line token are left alone: there the trailing whitespace is part of a
	///   string's value, not layout, and trimming it would silently change what the code returns.
	///   Only a literal can span lines as a single token, so no kind list is needed - a comment is
	///   trivia, and trimming inside one is harmless anyway.
	/// </remarks>
	private static string TrimTrailingWhitespace(string source, FormattingOptions formatting)
	{
		var unit = ParseCompilationUnit(source);
		var text = unit.GetText();
		var preserved = new HashSet<int>();

		foreach (var token in unit.DescendantTokens())
		{
			var span = text.Lines.GetLinePositionSpan(token.Span);

			// The literal's last line ends with the closing quote, so its trailing whitespace is
			// outside the value and may still be trimmed.
			for (var line = span.Start.Line; line < span.End.Line; line++)
			{
				preserved.Add(line);
			}
		}

		var lines = source.Split('\n');

		for (var i = 0; i < lines.Length; i++)
		{
			if (preserved.Contains(i))
			{
				continue;
			}

			// The '\r' of a CRLF pair is part of the separator, not trailing whitespace, so it is
			// stripped first and the configured separator is re-applied by the join below.
			lines[i] = lines[i].TrimEnd('\r').TrimEnd(' ', '\t');
		}

		return String.Join(formatting.EndOfLine, lines);
	}

	/// <summary>
	///   Renders one of the generator's hand-written source templates with the configured
	///   indentation, line endings and namespace style.
	/// </summary>
	/// <param name="compilation">The compilation the file belongs to.</param>
	/// <param name="formatting">The formatting settings to render with.</param>
	/// <param name="usings">The using directives, one per line, at column 0.</param>
	/// <param name="namespaceName">The namespace to declare.</param>
	/// <param name="body">The type declarations, at column 0.</param>
	/// <remarks>
	///   Deliberately textual rather than parse-and-reprint: <c>NormalizeWhitespace</c> discards
	///   blank lines, which are load-bearing for readability in code that was written by hand. The
	///   template is split into sections instead, so the namespace style only decides how deep the
	///   body is indented and the body's own layout survives untouched.
	/// </remarks>
	private static string RenderTemplate(Compilation compilation, FormattingOptions formatting, string usings, string namespaceName, string body)
	{
		var code = new IndentedCodeWriter(compilation, formatting);

		// The raw string literals carry no trailing newline, so each section has to be closed
		// explicitly: once to end its last line, and once more for the blank separator line.
		code.Write(Reindent(usings, formatting), true);
		code.WriteLine();
		code.WriteLine();

		IndentedCodeWriter.Block namespaceBlock = default;

		if (formatting.NamespaceDeclarations == NamespaceStyle.BlockScoped)
		{
			code.WriteLine($"namespace {namespaceName:literal}");
			namespaceBlock = code.WriteBlock();
		}
		else
		{
			code.WriteLine($"namespace {namespaceName:literal};");
			code.WriteLine();
		}

		code.Write(Reindent(LayoutTemplateBody(body, formatting), formatting), true);
		code.WriteLine();

		// A no-op when the namespace is file-scoped: a default Block has no writer to close.
		namespaceBlock.Dispose();

		return code.ToString();
	}

	/// <summary>
	///   Applies the token-level layout passes to a template body.
	/// </summary>
	/// <remarks>
	///   Deliberately without <c>NormalizeWhitespace</c>: the three passes are incremental trivia
	///   edits that work on any well-laid-out tree, while normalizing would discard the blank lines
	///   the template was written with. The passes run against a tab indentation unit because that
	///   is what the template literally contains; <see cref="Reindent" /> converts the result to the
	///   configured unit afterwards.
	/// </remarks>
	private static string LayoutTemplateBody(string body, FormattingOptions formatting)
	{
		var tabbed = formatting with { IndentationString = "\t" };

		var result = BracePlacementRewriter.Apply(ParseCompilationUnit(body), tabbed);

		result = SpacingRewriter.Apply(result, tabbed);
		result = IndentationRewriter.Apply(result, tabbed);

		return result.ToFullString();
	}

	/// <summary>
	///   Rewrites the leading tabs of every line into the configured indentation unit.
	/// </summary>
	/// <remarks>
	///   Only leading tabs are touched, and the templates use tabs purely for indentation - none of
	///   them contains a tab inside a string or character literal, where a blind replace would
	///   change the value rather than the layout.
	/// </remarks>
	private static string Reindent(string text, FormattingOptions formatting)
	{
		if (formatting.IndentationString == "\t")
		{
			return text;
		}

		var lines = text.Split('\n');

		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i];
			var depth = 0;

			while (depth < line.Length && line[depth] == '\t')
			{
				depth++;
			}

			if (depth > 0)
			{
				lines[i] = String.Concat(Repeat(formatting.IndentationString, depth), line.Substring(depth));
			}
		}

		return String.Join("\n", lines);
	}

	/// <summary>
	///   Indents every non-empty line of <paramref name="text" /> by <paramref name="levels" /> tabs,
	///   so that members interpolated into a raw string literal - which always arrive at column 0 -
	///   line up with the type that contains them.
	/// </summary>
	private static string Indent(string text, int levels)
	{
		var prefix = Repeat("\t", levels);

		return String.Join("\n", text
			.Split('\n')
			.Select(line => line.Length == 0
				? line
				: prefix + line));
	}

	private static string Repeat(string value, int count)
	{
		switch (count)
		{
			case <= 0:
			{
				return String.Empty;
			}
			case 1:
			{
				return value;
			}
		}

		var builder = StringBuilderCache.Acquire(value.Length * count);

		for (var i = 0; i < count; i++)
		{
			builder.Append(value);
		}

		return StringBuilderCache.GetStringAndRelease(builder);
	}

	private static bool InvokesVectorOperationsMethod(SyntaxNode? node, string methodName)
	{
		if (node is null)
		{
			return false;
		}

		var target = $"VectorOperations.{methodName}";

		return node.DescendantNodesAndSelf()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => invocation.Expression is GenericNameSyntax generic && generic.Identifier.Text == target);
	}

	private (string FileName, string Source)? GenerateMethodSource(Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel?> methodGroup, MetadataLoader loader, FormattingOptions formatting)
	{
		var code = new IndentedCodeWriter(compilation, formatting);

		var distinctUsings = methodGroup
			.SelectMany(m => m?.Usings ?? [ ])
			.ToSet();

		var distinctAdditionalMethods = methodGroup
			.SelectMany(m => m?.Additionalitems)
			.Distinct(SyntaxNodeComparer.Get())
			.ToList();

		var usings = OrderUsings(distinctUsings, formatting);
		var isInsideNamespace = formatting.UsingDirectivePlacement == UsingPlacement.InsideNamespace;

		// The file is assembled as text rather than as a syntax tree, so the namespace style is
		// emitted directly instead of going through ConvertNamespaceRefactoring; a block-scoped
		// namespace gets its indentation for free from the writer's own block handling.
		var isBlockScoped = formatting.NamespaceDeclarations == NamespaceStyle.BlockScoped;

		code.WriteLine();

		if (!isInsideNamespace)
		{
			WriteUsings(code, usings, formatting);
		}

		IndentedCodeWriter.Block namespaceBlock = default;

		if (isBlockScoped)
		{
			code.WriteLine("namespace ConstantExpression.Generated");
			namespaceBlock = code.WriteBlock();
		}
		else
		{
			code.WriteLine("namespace ConstantExpression.Generated;");
			code.WriteLine();
		}

		if (isInsideNamespace)
		{
			WriteUsings(code, usings, formatting);
		}

		// Emit top-level generated methods grouped by value.
		using (code.WriteBlock($"file static class {methodGroup.First().ParentType.Identifier:literal}"))
		{
			foreach (var additionalMethod in distinctAdditionalMethods.OfType<FieldDeclarationSyntax>().GroupBy(g => g.Declaration.Type, SyntaxNodeComparer.Get<TypeSyntax>()))
			{
				foreach (var item in additionalMethod)
				{
					code.WriteLine(FormattingHelper.Render(item, formatting), true);
				}

				code.WriteLine();
			}

			EmitGeneratedMethodsForValueGroups(code, compilation, methodGroup, loader);

			foreach (var additionalMethod in distinctAdditionalMethods)
			{
				code.WriteLine();
				// Format at emission time to avoid expensive NormalizeWhitespace during processing
				code.WriteLine(FormattingHelper.Render(additionalMethod, formatting), true);
			}
		}

		// A no-op when the namespace is file-scoped: a default Block has no writer to close.
		namespaceBlock.Dispose();

		var fileName = $"{methodGroup.First().ParentType.Identifier}_{methodGroup.Key.Identifier}.g.cs";

		return (fileName, code.ToString());
	}

	/// <summary>
	///   Orders the using directives per <c>dotnet_sort_system_directives_first</c>.
	/// </summary>
	private static List<string> OrderUsings(IEnumerable<string?> usings, FormattingOptions formatting)
	{
		var result = new List<string>();

		foreach (var item in usings)
		{
			if (!String.IsNullOrWhiteSpace(item))
			{
				result.Add(item!);
			}
		}

		result.Sort(formatting.SortSystemDirectivesFirst
			? UsingComparer.Instance
			: StringComparer.Ordinal);

		return result;
	}

	/// <summary>
	///   Writes the using directives followed by a blank separator line, inserting blank lines
	///   between groups when <c>dotnet_separate_import_directive_groups</c> asks for it.
	/// </summary>
	private static void WriteUsings(IndentedCodeWriter code, List<string> usings, FormattingOptions formatting)
	{
		string? previousGroup = null;

		foreach (var item in usings)
		{
			if (formatting.SeparateImportDirectiveGroups)
			{
				// The group is the first segment of the namespace, so System.* stays together and
				// is separated from an unrelated root such as ConstantExpression.*.
				var separator = item.IndexOf('.');
				var group = separator < 0 ? item : item.Substring(0, separator);

				if (previousGroup is not null && !String.Equals(previousGroup, group, StringComparison.Ordinal))
				{
					code.WriteLine();
				}

				previousGroup = group;
			}

			code.WriteLine($"using {item:literal};");
		}

		code.WriteLine();
	}

	#region Emission Helpers

	private void EmitGeneratedMethodsForValueGroups(IndentedCodeWriter code, Compilation compilation, IEnumerable<InvocationModel?> methodGroup, MetadataLoader loader)
	{
		var wroteFirstGroup = false;

		var invocations = methodGroup
			.Where(w => w?.Location is not null)
			.OrderBy(m => m!.Method.Span.Length)
			.GroupBy(m => m.Method.Identifier.ValueText, StringComparer.Ordinal);

		foreach (var invocationsByValue in invocations)
		{
			if (wroteFirstGroup)
			{
				code.WriteLine();
			}

			wroteFirstGroup = true;

			if (invocationsByValue.Any(a => a?.AttributeData.MathOptimizations != FastMathFlags.Strict))
			{
				// code.WriteLine("[MethodImpl(MethodImplOptions.AggressiveOptimization)]");
			}

			// Add interceptor attributes for every invocation (location based) that shares the same value.
			foreach (var invocationModel in invocationsByValue)
			{
				code.WriteLine($"[InterceptsLocation({invocationModel.Location.Version}, {invocationModel.Location.Data})]");
			}

			code.WriteLine(invocationsByValue.First().Method.ToFullString(), true);
		}
	}

	#endregion

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation
		    || !TryGetSymbol(context.SemanticModel, invocation, token, out var methodSymbol)
		    || !methodSymbol.IsStatic)
		{
			return null;
		}

		var attributes = methodSymbol.GetAttributes()
			.Concat(methodSymbol.ContainingType?.GetAttributes() ?? Enumerable.Empty<AttributeData>())
			.Concat(methodSymbol.ContainingAssembly.GetAttributes());

		var attribute = attributes.FirstOrDefault(IsAttribute<ConstEvalAttribute>)
		                ?? attributes.FirstOrDefault(IsAttribute<ConstExprAttribute>);

		// Check for ConstExprAttribute on type or method
		// Store minimal info here; defer heavy MetadataLoader creation until RegisterSourceOutput
		if (attribute is not null
		    && !IsInConstEvalBody(context.SemanticModel.Compilation, invocation)
		    && !IsInConstExprBody(context.SemanticModel.Compilation, invocation))
		{
			// Note: We skip IsContainingMethodInvoked check here since we don't have RoslynApiCache yet
			// This check will be done later in RegisterSourceOutput with the shared cache
			// Return a marker model to be processed later with shared MetadataLoader and RoslynApiCache
			return new InvocationModel
			{
				Invocation = invocation,
				MethodSymbol = methodSymbol,
				AttributeData = attribute.ToAttribute<ConstExprAttribute>(),
				CacheKey = $"{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}:{invocation.Span.Length}:{methodSymbol.OriginalDefinition}"
			};
		}

		return null;
	}

	public static InvocationModel? GenerateExpression(SemanticModel semanticModel, MetadataLoader loader, InvocationExpressionSyntax invocation,
	                                                  IMethodSymbol methodSymbol, ConstExprAttribute attribute, RoslynApiCache apiCache, FormattingOptions formatting,
	                                                  CancellationToken token)
	{
		var methodDecl = GetMethodSyntaxNode(methodSymbol);

		if (methodDecl == null)
		{
			return null;
		}

		var exceptions = new ConcurrentDictionary<SyntaxNode?, Exception>(SyntaxNodeComparer.Get());
		var symbolStore = new ConcurrentDictionary<ulong, ISymbol>();

		var visitor = new ConstExprOperationVisitor(semanticModel, loader, (operation, ex) =>
		{
			// exceptions.TryAdd(operation!.Syntax, ex);
		}, token);

		if ( //exceptions.IsEmpty
		    semanticModel.Compilation.TryGetSemanticModel(methodDecl, out var model))
		{
			var usings = new HashSet<string?>
			{
				"System.Runtime.CompilerServices",
				"System"
			};

			// var variables = ProcessArguments(visitor, context.SemanticModel.Compilation, invocation, loader, token);
			var variablesPartial = ProcessArguments(visitor, semanticModel, invocation, loader, apiCache, token);
			var additionalItems = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer.Get());

			var analyzer = new InlineVariableAnalyzer(model, symbolStore);
			var candidates = analyzer.FindInlineCandidates(methodDecl.Body!);

			foreach (var candidate in candidates)
			{
				var name = candidate.Symbol.Name;

				if (variablesPartial.TryGetValue(name, out var variable))
				{
					variable.CanBeInlined = true;
				}
				else
				{
					variablesPartial.Add(name, new VariableItem(
						candidate.Symbol.Type, // Type is not needed for inlining, as the value will be directly substituted
						false,
						null,
						candidate.Symbol.NullableAnnotation == NullableAnnotation.Annotated && !candidate.Symbol.Type.IsValueType)
					{
						CanBeInlined = true
					});
				}
			}

			var partialVisitor = new ConstExprPartialRewriter(model, loader, (node, ex) =>
			{
				exceptions.TryAdd(node, ex);
			}, variablesPartial, additionalItems, usings, attribute, symbolStore, token);

			var timer = Stopwatch.StartNew();

			// Try full constant evaluation first: if all arguments are known constants and the
			// operation visitor can evaluate the entire method body to a literal, use it directly
			// rather than the partially-specialized body.
			var result = TryFullyEvaluateMethod(model, loader, methodSymbol, variablesPartial, token, methodDecl)
			             ?? partialVisitor.VisitBlock(methodDecl.Body); // partialVisitor.VisitBlock(blockOperation.BlockBody!, variablesPartial);
			var result2 = DeadCodePruner.Prune(result, variablesPartial, semanticModel);
			result2 = ExceptionGuardSimplifier.Simplify(result2);

			result2 = OptimizationPipeline.Apply(result2, methodDecl.ParameterList, methodDecl.Identifier, attribute, variablesPartial, semanticModel, symbolStore, additionalItems, usings!);

			// Format using Roslyn formatter instead of NormalizeWhitespace
			// var text = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result));
			// var text2 = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result2));

			timer.Stop();

			// Keep generating an interceptor even when the rewritten body is identical.
			// This ensures every invocation site can still be intercepted safely, including
			// mixed/non-constant calls where partial evaluation yields a passthrough body.

			GetUsings(model, methodDecl, methodSymbol, usings);

			if (attribute.MathOptimizations != FastMathFlags.Strict || attribute.Optimizations != OptimizationFlags.None)
			{
				usings.Add("System.Runtime.CompilerServices");
			}

			// MemoryMarshal.GetArrayDataReference, emitted by the bounds-check pass.
			if (attribute.Optimizations.HasFlag(OptimizationFlags.BoundsCheckElimination))
			{
				usings.Add("System.Runtime.InteropServices");
			}

			var resultMethod = methodDecl
				.WithoutLeadingTrivia()
				.WithIdentifier(Identifier($"{methodDecl.Identifier.Text}_{result2.GetDeterministicHashString()}")
					.WithLeadingTrivia(methodDecl.Identifier.LeadingTrivia)
					.WithTrailingTrivia(methodDecl.Identifier.TrailingTrivia));

			if (result2 is BlockSyntax methodBody)
			{
				// Collapse a `return cond ? a : cond2 ? b : default;` chain into a switch expression.
				if (methodBody.Statements is [ ReturnStatementSyntax { Expression: ConditionalExpressionSyntax chain } returnStatement ]
				    && ConvertIfToSwitchCodeRefactoring.TryConvertConditionalChainToSwitch(chain, out var switchExpression))
				{
					methodBody = methodBody.WithStatements(
						SingletonList<StatementSyntax>(returnStatement.WithExpression(switchExpression)));
				}

				if (formatting.ExpressionBodiedMethods != ExpressionBodyPreference.Never
				    && methodBody.Statements is [ ReturnStatementSyntax { Expression: var returnExpression } ]
				    && CanBeExpressionBody(returnExpression))
				{
					resultMethod = resultMethod
						.WithBody(null)
						.WithExpressionBody(ArrowExpressionClause(returnExpression).WithTrailingTrivia())
						.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
				}
				else
				{
					resultMethod = resultMethod.WithBody(methodBody);
				}
			}

			return new InvocationModel
			{
				Usings = usings!,
				OriginalMethod = methodDecl,
				Method = FormattingHelper.Format(resultMethod, formatting) as MethodDeclarationSyntax ?? resultMethod,
				// Defer formatting to emission time to avoid expensive NormalizeWhitespace calls
				Additionalitems = additionalItems
					.OrderByDescending(o => o.Value)
					.Select(s => s.Key),
				ParentType = methodDecl.Parent as TypeDeclarationSyntax,
				Invocation = invocation,
				Location = semanticModel.GetInterceptableLocation(invocation, token),
				Exceptions = exceptions!,
				AttributeData = attribute
			};
		}

		return null;

		bool CanBeExpressionBody(SyntaxNode? node)
		{
			return node switch
			{
				LiteralExpressionSyntax or CollectionExpressionSyntax => true,
				PrefixUnaryExpressionSyntax prefixUnaryExpression => CanBeExpressionBody(prefixUnaryExpression.Operand),
				_ => false
			};
		}
	}

	private static SyntaxNode? TryFullyEvaluateMethod(
		SemanticModel model,
		MetadataLoader loader,
		IMethodSymbol methodSymbol,
		IDictionary<string, VariableItem> variablesPartial,
		CancellationToken token,
		MethodDeclarationSyntax methodDecl)
	{
		var constantValues = variablesPartial.Values
			.Where(v => v.HasValue)
			.Select(v => v.Value)
			.ToList();

		if (constantValues.Count != methodSymbol.Parameters.Length)
		{
			return null;
		}

		if (!TryGetOperation<IOperation>(model, methodSymbol, out var fullMethodOp))
		{
			return null;
		}

		var paramList = fullMethodOp.Syntax switch
		{
			MethodDeclarationSyntax m => m.ParameterList,
			LocalFunctionStatementSyntax lf => lf.ParameterList,
			_ => null
		};

		if (paramList is null || paramList.Parameters.Count != constantValues.Count)
		{
			return null;
		}

		var fullVars = new Dictionary<string, object?>();

		for (var pi = 0; pi < paramList.Parameters.Count; pi++)
		{
			fullVars[paramList.Parameters[pi].Identifier.Text] = constantValues[pi];
		}

		var fullVisitor = new ConstExprOperationVisitor(model, loader, (_, _) => { }, token);

		try
		{
			switch (fullMethodOp)
			{
				case ILocalFunctionOperation { Body: not null } lf:
					fullVisitor.VisitBlock(lf.Body, fullVars);
					break;
				case IMethodBodyOperation { BlockBody: not null } mb:
					fullVisitor.VisitBlock(mb.BlockBody, fullVars);
					break;
				default:
					return null;
			}

			if (fullVars.TryGetValue(ConstExprOperationVisitor.RETURNVARIABLENAME, out var returnValue)
			    && returnValue is not null
			    && TryCreateLiteral(returnValue, out var literalResult))
			{
				return Block(ReturnStatement(literalResult));
			}
		}
		catch
		{
			// Fall through to partial evaluation
		}

		return null;
	}

	public static Dictionary<string, VariableItem> ProcessArguments(ConstExprOperationVisitor visitor, SemanticModel model, InvocationExpressionSyntax invocation, MetadataLoader loader, RoslynApiCache apiCache, CancellationToken token)
	{
		var variables = new Dictionary<string, VariableItem>();

		// Use cached GetOperation result
		var invocationOperation = apiCache.GetOrAddOperation(invocation, model, token) as IInvocationOperation;
		var methodSymbol = invocationOperation?.TargetMethod;

		if (invocationOperation is null || methodSymbol is null)
		{
			return variables;
		}

		foreach (var argument in invocationOperation.Arguments)
		{
			if (loader.GetType(argument.Parameter.Type).IsEnum)
			{
				try
				{
					var enumType = loader.GetType(argument.Parameter.Type);
					var value = visitor.Visit(argument.Value, new VariableItemDictionary(variables));

					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type, true, Enum.ToObject(enumType, value), true));
				}
				catch (Exception)
				{
					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type ?? argument.Parameter.Type, false, null, true));
				}
			}
			else
			{
				var canBeNull = argument.Parameter.NullableAnnotation == NullableAnnotation.Annotated && !argument.Parameter.Type.IsValueType;

				try
				{
					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type ?? argument.Parameter.Type, true, visitor.Visit(argument.Value, new VariableItemDictionary(variables)), true) { CanBeNull = canBeNull });
				}
				catch (Exception)
				{
					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type ?? argument.Parameter.Type, false, argument.Syntax, true) { CanBeNull = canBeNull });
				}
			}
		}

		foreach (var (parameter, argument) in methodSymbol.TypeParameters.Zip(methodSymbol.TypeArguments, (x, y) => (x, y)))
		{
			var canBeNull = parameter.NullableAnnotation == NullableAnnotation.Annotated && !parameter.IsValueType;
			variables.Add($"#{parameter.Name}", new VariableItem(argument, true, loader.GetType(argument), true) { CanBeNull = canBeNull });
		}

		return variables;
	}

	private static MethodDeclarationSyntax? GetMethodSyntaxNode(IMethodSymbol methodSymbol)
	{
		return methodSymbol.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax())
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault();
	}

	private static void GetUsings(SemanticModel model, MethodDeclarationSyntax methodDecl, IMethodSymbol methodSymbol, ISet<string?> usings)
	{
		SetUsings(methodSymbol.ReturnType, usings);

		foreach (var p in methodSymbol.Parameters)
		{
			SetUsings(p.Type, usings);
		}

		foreach (var type in methodSymbol.TypeParameters.SelectMany(s => s.ConstraintTypes))
		{
			SetUsings(type, usings);
		}

		// Types referenced only inside the body (local variable types, casts, default/typeof,
		// pattern types, ...) never show up in the signature above, but AsTypeSyntax() emits
		// them by bare name regardless - so scan every type reference in the original,
		// still-bound body too. Extra usings for types the rewriter later folds away are
		// harmless; a missing one is a CS0246 in the generated file.
		foreach (var typeSyntax in methodDecl.DescendantNodes().OfType<TypeSyntax>())
		{
			if (model.GetSymbolInfo(typeSyntax).Symbol is ITypeSymbol typeSymbol)
			{
				SetUsings(typeSymbol, usings);
			}
		}
	}

	private static void SetUsings(ITypeSymbol type, ISet<string?> usings)
	{
		if (!type.IsPrimitiveType())
		{
			usings.Add(type.ContainingNamespace?.ToString());
		}

		switch (type)
		{
			case INamedTypeSymbol namedType:
			{
				foreach (var arg in namedType.TypeArguments)
				{
					SetUsings(arg, usings);
				}
				break;
			}
			case IArrayTypeSymbol arrayType:
			{
				SetUsings(arrayType.ElementType, usings);
				break;
			}
		}
	}

	private static bool TryGetSymbol(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken token, [NotNullWhen(true)] out IMethodSymbol? symbol)
	{
		if (semanticModel.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol s)
		{
			symbol = s;
			return true;
		}

		var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
		var symbols = semanticModel.LookupSymbols(invocation.SpanStart, semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType);

		foreach (var item in symbols)
		{
			if (item is IMethodSymbol { IsStatic: true } methodSymbol && methodSymbol.Name == memberAccess?.Name.ToString())
			{
				symbol = methodSymbol;
				return true;
			}
		}

		symbol = null;
		return false;
	}

	private void ReportExceptions(SgfSourceProductionContext spc, IEnumerable<InvocationModel> models)
	{
		// Only report exceptions for invocations that did NOT successfully evaluate and inject an intercept location
		var exceptions = models
			.Where(m => m?.Location == null)
			.SelectMany(m => m.Exceptions.Select(s => s.Key))
			.Distinct(SyntaxNodeComparer.Get());

		var exceptionDescriptor = new DiagnosticDescriptor(
			"CEA005",
			"Exception during evaluation",
			"Unable to evaluate: {0}",
			"Usage",
			DiagnosticSeverity.Warning,
			true);

		foreach (var exception in exceptions)
		{
			if (exceptions.Any(a => a != exception && exception.Span.Contains(a.Span)))
			{
				continue;
			}

			spc.ReportDiagnostic(Diagnostic.Create(exceptionDescriptor, exception.GetLocation(), exception));
		}
	}
}

#pragma warning restore RSEXPERIMENTAL002