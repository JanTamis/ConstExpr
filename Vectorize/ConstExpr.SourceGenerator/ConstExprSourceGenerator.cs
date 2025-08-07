using ConstExpr.SourceGenerator.Builders;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using SourceGen.Utilities.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ConstExpr.SourceGenerator.Comparers;
using SGF;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

[assembly: InternalsVisibleTo("ConstExpr.Tests")]

namespace ConstExpr.SourceGenerator;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public class ConstExprSourceGenerator() : IncrementalGenerator("ConstExpr")
{
	private const bool ShouldGenerate = true;

	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(spc =>
		{
			spc.AddSource("GenerationLevel.g", """
				using System;

				namespace ConstantExpression
				{
					public enum GenerationLevel
					{
						Minimal,
						Balanced,
						Performance,
					}
				}
				""");

			spc.AddSource("ConstExprAttribute.g", $$"""
				using System;

				namespace ConstantExpression
				{
					[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
					public sealed class ConstExprAttribute : Attribute
					{
						public GenerationLevel Level { get; set; } = GenerationLevel.Balanced;
					}
				}
				""");


			spc.AddSource("InterceptsLocationAttribute.g", """
				using System;
				using System.Diagnostics;

				namespace System.Runtime.CompilerServices
				{
					[Conditional("DEBUG")]
					[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
					internal sealed class InterceptsLocationAttribute : Attribute
					{
						public InterceptsLocationAttribute(int version, string data)
						{
							_ = version;
							_ = data;
						}
					}
				}
				""");
		});

		var invocations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: (node, token) => !token.IsCancellationRequested && node is InvocationExpressionSyntax,
				transform: GenerateSource)
			.Where(result => result != null);

		var rootNamespace = context
			.AnalyzerConfigOptionsProvider
			// Retrieve the RootNamespace property
			.Select((c, _) =>
				c.GlobalOptions.TryGetValue("build_property.ConstExprGenerationLevel", out var level)
					? level
					: null);

		context.RegisterSourceOutput(invocations.Collect().Combine(context.CompilationProvider).Combine(rootNamespace), (spc, modelAndCompilation) =>
		{
			var loader = MetadataLoader.GetLoader(modelAndCompilation.Left.Right);
				
			foreach (var group in modelAndCompilation.Left.Left.GroupBy(model => model.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
			{
				GenerateMethodImplementations(spc, modelAndCompilation.Left.Right, group, loader);
			}

			ReportExceptions(spc, modelAndCompilation.Left.Left);
		});
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel?> group, MetadataLoader loader)
	{
		var code = new IndentedCodeWriter(compilation);
		var usings = group.SelectMany(item => item.Usings).Distinct().OrderBy(s => s);

		foreach (var u in usings.Where(w => !String.IsNullOrWhiteSpace(w)))
		{
			code.WriteLine($"using {u:literal};");
		}

		code.WriteLine();
		code.WriteLine("namespace ConstantExpression.Generated;");
		code.WriteLine();

		using (code.WriteBlock($"file static class GeneratedMethods", "{", "}"))
		{
			var isFirst = true;

			foreach (var valueGroup in group.GroupBy(m => m.Value, new ValueOrCollectionEqualityComparer()))
			{
				code.WriteLineIf(!isFirst);

				isFirst = false;

				// Add interceptor attributes
				foreach (var item in valueGroup)
				{
					code.WriteLine($"[InterceptsLocation({item.Location.Version}, {item.Location.Data})]");
				}

				// Generate the method implementation
				var first = valueGroup.First();

				var method = first.Method
					.WithIdentifier(SyntaxFactory.Identifier($"{first.Method.Identifier}_{Math.Abs(first.Value?.GetHashCode() ?? 0)}"));

				if (first.Symbol.ReturnType is IArrayTypeSymbol arraySymbol)
				{
					var valueType = first.Value?.GetType();

					if (valueType is not null && valueType.IsArray)
					{
						var elementType = valueType.GetElementType();
						var rank = valueType.GetArrayRank();

						if (elementType?.FullName == arraySymbol.ElementType.ToDisplayString() && rank == arraySymbol.Rank)
						{
							method = method.WithReturnType(
								SyntaxFactory.ParseTypeName(valueType.Name)
									.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "))
							);
						}
					}
				}

				using (code.WriteBlock(method))
				{
					if (compilation.IsInterface(first.Method.ReturnType))
					{
						if (IsIEnumerable(compilation, first.Method.ReturnType) && first.Value is IEnumerable enumerable)
						{
							var returnType = compilation.GetSemanticModel(first.Method.SyntaxTree).GetTypeInfo(first.Method.ReturnType).Type;

							if (returnType is not INamedTypeSymbol elementType)
							{
								continue;
							}

							if (compilation.TryGetIEnumerableType(elementType, true, out var tempElementype) && tempElementype is INamedTypeSymbol)
							{
								elementType = tempElementype as INamedTypeSymbol;
							}

							var data = enumerable.Cast<object?>().ToArray();

							if (data.IsSame(data[0]))
							{
								code.WriteLine($"return Enumerable.Repeat({CreateLiteral(data[0])}, {CreateLiteral(data.Length)})");
							}
							else if (data.IsNumericSequence() && compilation.IsSpecialType(elementType, SpecialType.System_Int32))
							{
								code.WriteLine($"return Enumerable.Range({CreateLiteral(data[0])}, {CreateLiteral(data.Length)})");
							}
							else if (elementType.IsVectorSupported() && data.IsSequenceDifference(out var difference))
							{
								if (compilation.GetTypeByName(typeof(Enumerable).FullName).HasMethod("Sequence"))
								{
									code.WriteLine($"return Enumerable.Sequence({CreateLiteral(data[0])}, {CreateLiteral(data[^1])}, {CreateLiteral(difference)});");
								}
								else
								{
									code.WriteLine($$"""
										var start = {{data[0]}};

										do
										{
											yield return start;
											start += {{difference}};
										}
										while (start <= {{data[^1]}});
										""");
								}
							}
							else
							{
								foreach (var item in data)
								{
									code.WriteLine($"yield return {item};");
								}
							}
						}
						else
						{
							var name = first.Method.ReturnType is GenericNameSyntax genericName
								? genericName.Identifier.Text
								: first.Method.ReturnType.ToString();

							code.WriteLine($"return {name:literal}_{Math.Abs(first.Value?.GetHashCode() ?? 0)}.Instance;");
						}
					}
					else if (TryGetLiteral(first.Value, out var literal))
					{
						code.WriteLine($"return {literal};");
					}
				}
			}
		}

		foreach (var invocation in group.DistinctBy(d => d.Value))
		{
			var hashCode = Math.Abs(invocation.Value?.GetHashCode() ?? 0);

			if (compilation.IsInterface(invocation.Method.ReturnType) && !IsIEnumerable(compilation, invocation.Method.ReturnType))
			{
				var returnType = compilation.GetSemanticModel(invocation.Method.SyntaxTree).GetTypeInfo(invocation.Method.ReturnType).Type;

				if (returnType is not INamedTypeSymbol namedTypeSymbol)
				{
					continue;
				}

				var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
				// var elementName = compilation.GetMinimalString(elementType);
				var dataName = $"{namedTypeSymbol.Name}_{hashCode}_Data";

				IEnumerable<string> interfaces = [ compilation.GetMinimalString(returnType) ];

				code.WriteLine();

				using (code.WriteBlock($"file sealed class {namedTypeSymbol.Name:literal}_{hashCode} : {String.Join(", ", interfaces):literal}"))
				{
					code.WriteLine($"public static {namedTypeSymbol.Name:literal}_{hashCode} Instance = new {namedTypeSymbol.Name:literal}_{hashCode}();");

					if (invocation.Value is IEnumerable enumerable)
					{
						elementType ??= enumerable
							.Cast<object?>()
							.Where(w => w is not null)
							.Select(s => compilation.GetTypeByType(s.GetType()))
							.First();

						// elementName ??= compilation.GetMinimalString(elementType);

						code.WriteLine();

						if (compilation.IsSpecialType(elementType, SpecialType.System_Char))
						{
							code.WriteLine($"public static ReadOnlySpan<{elementType}> {dataName:literal} => \"{String.Join(String.Empty, enumerable.Cast<object?>()):literal}\";");
						}
						else
						{
							if (elementType.IsVectorSupported())
							{
								code.WriteLine($"public static ReadOnlySpan<{elementType}> {dataName:literal} => [{enumerable}];");
							}
							else
							{
								code.WriteLine($"public static {elementType}[] {dataName:literal} = [{enumerable}];");
							}
						}

						if (elementType is not null)
						{
							var items = enumerable
								.Cast<object?>()
								.ToImmutableArray();

							var members = namedTypeSymbol.AllInterfaces
								.Prepend(namedTypeSymbol)
								.SelectMany(s => s.GetMembers())
								.Distinct(SymbolEqualityComparer.Default)
								.OrderBy(o => o is not IPropertySymbol);

							var interfaceBuilder = new InterfaceBuilder(compilation, loader, elementType, invocation.GenerationLevel, dataName);
							var enumerableBuilder = new EnumerableBuilder(compilation, elementType, loader, invocation.GenerationLevel, dataName);
							var memoryExtensionsBuilder = new MemoryExtensionsBuilder(compilation, loader, elementType, invocation.GenerationLevel, dataName);

							foreach (var member in members)
							{
								if (TryWrite(code, member, items, enumerable, interfaceBuilder, enumerableBuilder, memoryExtensionsBuilder))
								{
									continue;
								}

								if (!IsIEnumerableRecursive(namedTypeSymbol))
								{
									var descriptor = new DiagnosticDescriptor(
										"CEA006",
										"Unable to implement {0}",
										"Unable to implement {0}",
										"Usage",
										DiagnosticSeverity.Error,
										true);

									spc.ReportDiagnostic(Diagnostic.Create(descriptor, member.Locations[0], member.Name));
								}
							}

							if (IsIEnumerableRecursive(namedTypeSymbol))
							{
								code.WriteLine();

								using (code.WriteBlock($"public IEnumerator<{elementType}> GetEnumerator()"))
								{
									if (items.IsEmpty)
									{
										code.WriteLine($"return Enumerable.Empty<{elementType}>().GetEnumerator();");
									}
									else if (elementType.IsVectorSupported())
									{
										using (code.WriteBlock($"for (var i = 0; i < {dataName:literal}.Length; i++)"))
										{
											code.WriteLine($"yield return {dataName:literal}[i];");
										}
									}
									else
									{
										code.WriteLine($"return Array.AsReadOnly({dataName:literal}).GetEnumerator();");
									}
								}

								code.WriteLine();

								using (code.WriteBlock($"IEnumerator IEnumerable.GetEnumerator()", "{", "}"))
								{
									code.WriteLine("return GetEnumerator();");
								}
							}
						}
					}
				}
			}
		}

		if (ShouldGenerate && group.Key.Parent is TypeDeclarationSyntax type && !group.Any(a => a.Exceptions.Any()))
		{
			spc.AddSource($"{type.Identifier}_{group.Key.Identifier}.g.cs", code.ToString());
		}
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation
		    || !TryGetSymbol(context.SemanticModel, invocation, token, out var method)
		    || !method.IsStatic)
		{
			return null;
		}

		var attribute = method.GetAttributes().FirstOrDefault(IsConstExprAttribute)
		                ?? (method.ContainingType is ITypeSymbol type ? type.GetAttributes().FirstOrDefault(IsConstExprAttribute) : null);

		// Check for ConstExprAttribute on type or method
		if (attribute is not null)
		{
			var loader = MetadataLoader.GetLoader(context.SemanticModel.Compilation);

			var level = attribute.NamedArguments
				.Where(w => w.Key == "Level")
				.Select(s => (GenerationLevel) s.Value.Value)
				.DefaultIfEmpty(GenerationLevel.Balanced)
				.FirstOrDefault();

			return GenerateExpression(context, loader, invocation, method, level, token);
		}

		return null;
	}

	private InvocationModel? GenerateExpression(GeneratorSyntaxContext context, MetadataLoader loader, InvocationExpressionSyntax invocation,
	                                            IMethodSymbol methodSymbol, GenerationLevel level, CancellationToken token)
	{
		if (IsInConstExprBody(invocation))
		{
			return null;
		}

		var methodDecl = GetMethodSyntaxNode(methodSymbol);

		if (methodDecl == null)
		{
			return null;
		}

		var exceptions = new ConcurrentDictionary<SyntaxNode, Exception>(SyntaxNodeComparer<SyntaxNode>.Instance);

		var visitor = new ConstExprOperationVisitor(context.SemanticModel.Compilation, loader, (operation, ex) =>
		{
			exceptions.TryAdd(operation!.Syntax, ex);
		}, token);

		var variables = ProcessArguments(visitor, context.SemanticModel.Compilation, invocation, loader, token);

		if (variables == null)
		{
			return null;
		}

		if (TryGetOperation<IMethodBodyOperation>(context.SemanticModel.Compilation, methodDecl, out var blockOperation) &&
		    context.SemanticModel.Compilation.TryGetSemanticModel(invocation, out var model))
		{
			try
			{
				var timer = Stopwatch.StartNew();

				var usings = new HashSet<string?>
				{
					"System.Runtime.CompilerServices",
				};

				if (!methodSymbol.ReturnType.IsPrimitiveType() && !methodSymbol.IsAsync)
				{
					usings.Add("System.Numerics");
					usings.Add("System.Collections.Generic");
				}

				visitor.VisitBlock(blockOperation.BlockBody!, variables);
				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				GetUsings(methodSymbol, BaseBuilder.IsPerformance(level, (variables[ConstExprOperationVisitor.RETURNVARIABLENAME] as IEnumerable)?.Cast<object?>()?.Count() ?? 0), usings);

				return new InvocationModel
				{
					Usings = usings,
					Method = methodDecl,
					Symbol = methodSymbol,
					Invocation = invocation,
					Value = variables[ConstExprOperationVisitor.RETURNVARIABLENAME],
					Location = model.GetInterceptableLocation(invocation, token),
					Exceptions = exceptions,
					GenerationLevel = level,
				};
			}
			catch (Exception e)
			{
				Logger.Error(e, $"Error processing {invocation}: {e.Message}");

				return null;
			}
		}

		return null;
	}

	public static Dictionary<string, object?> ProcessArguments(ConstExprOperationVisitor visitor, Compilation compilation, InvocationExpressionSyntax invocation, MetadataLoader loader, CancellationToken token)
	{
		var variables = new Dictionary<string, object?>();
		var invocationOperation = compilation.GetSemanticModel(invocation.SyntaxTree).GetOperation(invocation) as IInvocationOperation;
		var methodSymbol = invocationOperation?.TargetMethod;

		foreach (var argument in invocationOperation.Arguments)
		{
			if (loader.GetType(argument.Parameter.Type).IsEnum)
			{
				var enumType = loader.GetType(argument.Parameter.Type);
				var value = visitor.Visit(argument.Value, variables);
				variables.Add(argument.Parameter.Name, Enum.ToObject(enumType, value));
			}
			else
			{
				variables.Add(argument.Parameter.Name, visitor.Visit(argument.Value, variables));
			}
		}

		foreach (var (parameter, argument) in methodSymbol.TypeParameters.Zip(methodSymbol.TypeArguments, (x, y) => (x, y)))
		{
			variables.Add(parameter.Name, loader.GetType(argument));
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

	private static void GetUsings(IMethodSymbol methodSymbol, bool isPerformance, HashSet<string?> usings)
	{
		if (isPerformance && methodSymbol.ReturnType.TypeKind == TypeKind.Interface)
		{
			usings.Add("System.Numerics");
			usings.Add("System.Collections");
			usings.Add("System.Runtime.InteropServices");
		}

		if (IsIEnumerableRecursive(methodSymbol.ReturnType as INamedTypeSymbol))
		{
			usings.Add("System.Collections");
			usings.Add("System.Linq");
		}

		if (!methodSymbol.ReturnType.IsPrimitiveType())
		{
			usings.Add(methodSymbol.ReturnType.ContainingNamespace?.ToString());
		}

		if (methodSymbol.ReturnType is INamedTypeSymbol namedTypeSymbol)
		{
			foreach (var type in namedTypeSymbol.TypeArguments)
			{
				if (!type.IsPrimitiveType())
				{
					usings.Add(type.ContainingNamespace?.ToString());
				}
			}
		}

		foreach (var p in methodSymbol.Parameters)
		{
			if (!p.Type.IsPrimitiveType())
			{
				usings.Add(p.Type.ContainingNamespace?.ToString());
			}
		}

		foreach (var type in methodSymbol.TypeParameters.SelectMany(s => s.ConstraintTypes))
		{
			if (!type.IsPrimitiveType())
			{
				usings.Add(type.ContainingNamespace?.ToString());
			}
		}

		if (!IsIEnumerable(methodSymbol.ReturnType) && methodSymbol.ReturnType.TypeKind == TypeKind.Interface)
		{
			usings.Add("System.Runtime.Intrinsics");
			usings.Add("System.Collections.Generic");
			usings.Add("System.Linq");

			foreach (var member in methodSymbol.ReturnType.GetMembers())
			{
				if (member is IMethodSymbol method)
				{
					GetUsings(method, false, usings);
				}

				usings.Add(member.ContainingNamespace?.ToString());
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
		var exceptions = models
			.SelectMany(m => m.Exceptions.Select(s => s.Key))
			.Distinct(SyntaxNodeComparer<SyntaxNode>.Instance);

		var exceptionDescriptor = new DiagnosticDescriptor(
			"CEA005",
			"Exception during evaluation",
			"Unable to evaluate: {0}",
			"Usage",
			DiagnosticSeverity.Warning,
			true);

		foreach (var exception in exceptions)
		{
			spc.ReportDiagnostic(Diagnostic.Create(exceptionDescriptor, exception.GetLocation(), exception));
		}
	}

	private static bool TryWrite(IndentedCodeWriter? code, ISymbol symbol, ImmutableArray<object?> items, IEnumerable enumerable, InterfaceBuilder interfaceBuilder, EnumerableBuilder enumerableBuilder, MemoryExtensionsBuilder memoryExtensionsBuilder)
	{
		switch (symbol)
		{
			case IPropertySymbol property
				when interfaceBuilder.AppendCount(property, items.Length, code)
				     || interfaceBuilder.AppendLength(property, items.Length, code)
				     || interfaceBuilder.AppendIsReadOnly(property, code)
				     || interfaceBuilder.AppendIndexer(property, items, code):
			case IMethodSymbol method
				when interfaceBuilder.AppendAdd(method, code)
				     || interfaceBuilder.AppendClear(method, code)
				     || interfaceBuilder.AppendRemove(method, code)
				     || interfaceBuilder.AppendRemoveAt(method, code)
				     || interfaceBuilder.AppendInsert(method, code)
				     || interfaceBuilder.AppendIndexOf(method, items, code)
				     || interfaceBuilder.AppendCopyTo(method, items, code)
				     || interfaceBuilder.AppendContains(method, items, code)
				     || interfaceBuilder.AppendCopyTo(method, items, code)
				     || interfaceBuilder.AppendOverlaps(method, items, code)
				     || enumerableBuilder.AppendAll(method, items, code)
				     || enumerableBuilder.AppendAggregate(method, items, code)
				     || enumerableBuilder.AppendAny(method, items, code)
				     || enumerableBuilder.AppendAverage(method, items, code)
				     || enumerableBuilder.AppendCount(method, items, code)
				     || enumerableBuilder.AppendDistinct(method, items, code)
				     || enumerableBuilder.AppendDistinctBy(method, items, code)
				     || enumerableBuilder.AppendElementAt(method, items, code)
				     || enumerableBuilder.AppendElementAtOrDefault(method, items, code)
				     || enumerableBuilder.AppendFirst(method, items, code)
				     || enumerableBuilder.AppendFirstOrDefault(method, items, code)
				     || enumerableBuilder.AppendLast(method, items, code)
				     || enumerableBuilder.AppendLastOrDefault(method, items, code)
				     || enumerableBuilder.AppendOrder(method, items, code)
				     || enumerableBuilder.AppendOrderDescending(method, items, code)
				     || enumerableBuilder.AppendSelect(method, items, code)
				     || enumerableBuilder.AppendSequenceEqual(method, items, code)
				     || enumerableBuilder.AppendSingle(method, items, code)
				     || enumerableBuilder.AppendSingleOrDefault(method, items, code)
				     || enumerableBuilder.AppendSum(method, items, code)
				     || enumerableBuilder.AppendWhere(method, items, code)
				     || enumerableBuilder.AppendToArray(method, items, code)
				     || enumerableBuilder.AppendToImmutableArray(method, items, code)
				     || enumerableBuilder.AppendToList(method, items, code)
				     || enumerableBuilder.AppendImmutableList(method, items, code)
				     || enumerableBuilder.AppendToHashSet(method, items, code)
				     || enumerableBuilder.AppendMax(method, items, code)
				     || enumerableBuilder.AppendMin(method, items, code)
				     || enumerableBuilder.AppendSkip(method, items, code)
				     || enumerableBuilder.AppendTake(method, items, code)
				     || enumerableBuilder.AppendCountBy(method, items, code)
				     || enumerableBuilder.AppendZip(method, items, code)
				     || enumerableBuilder.AppendChunk(method, items, code)
				     || enumerableBuilder.AppendExcept(method, items, code)
				     || enumerableBuilder.AppendExceptBy(method, items, code)
				     || memoryExtensionsBuilder.AppendBinarySearch(method, items, code)
				     || memoryExtensionsBuilder.AppendCommonPrefixLength(method, items, code)
				     || memoryExtensionsBuilder.AppendContainsAny(method, items, code)
				     // || memoryExtensionsBuilder.AppendContainsAnyExcept(method, items, code)
				     || memoryExtensionsBuilder.AppendContainsAnyInRange(method, items, code)
				     // || memoryExtensionsBuilder.AppendContainsAnyExceptInRange(method, items, code)
				     || memoryExtensionsBuilder.AppendCount(method, items, code)
				     || memoryExtensionsBuilder.AppendEndsWith(method, items, code)
				     || memoryExtensionsBuilder.AppendEnumerateLines(method, enumerable as string, code)
				     || memoryExtensionsBuilder.AppendEnumerableRunes(method, enumerable as string, code)
				     || memoryExtensionsBuilder.AppendIsWhiteSpace(method, enumerable as string, code)
				     // || memoryExtensionsBuilder.AppendIndexOfAny(method, items, code)
				     // || memoryExtensionsBuilder.AppendIndexOfAnyExcept(method, items, code)
				     // || memoryExtensionsBuilder.AppendIndexOfAnyExceptInRange(method, items, code)
				     // || memoryExtensionsBuilder.AppendIndexOfAnyInRange(method, items, code)
				     // || memoryExtensionsBuilder.AppendSequenceCompareTo(method, items, code)
				     || memoryExtensionsBuilder.AppendReplace(method, items, code):
				return true;
			default:
				return false;
		}
	}
}

#pragma warning restore RSEXPERIMENTAL002