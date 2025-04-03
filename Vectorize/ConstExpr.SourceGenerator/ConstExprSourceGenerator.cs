using ConstExpr.SourceGenerator.Builders;
using ConstExpr.SourceGenerator.Enums;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SGF;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

[assembly: InternalsVisibleTo("ConstExpr.Tests")]

namespace ConstExpr.SourceGenerator;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public class ConstExprSourceGenerator() : IncrementalGenerator("ConstExpr")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(spc =>
		{
			var invocations = context.SyntaxProvider
				.CreateSyntaxProvider(
					predicate: (node, token) => !token.IsCancellationRequested && node is InvocationExpressionSyntax,
					transform: GenerateSource)
				.Where(result => result != null);

			context.RegisterSourceOutput(invocations.Collect().Combine(context.CompilationProvider), (spc, modelAndCompilation) =>
			{
				foreach (var group in modelAndCompilation.Left.GroupBy(model => model.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
				{
					GenerateMethodImplementations(spc, modelAndCompilation.Right, group);
				}

				ReportExceptions(spc, modelAndCompilation.Left);
			});

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

			spc.AddSource("ConstExprAttribute.g", """
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
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel?> group)
	{
		var code = new IndentedStringBuilder();
		var usings = group.SelectMany(item => item.Usings).Distinct().OrderBy(s => s);

		using var loader = MetadataLoader.GetLoader(compilation);

		foreach (var u in usings)
		{
			code.AppendLine(u);
		}

		code.AppendLine();
		code.AppendLine("namespace ConstantExpression.Generated;");
		code.AppendLine();
		// code.AppendLine("{");
		code.AppendLine("file static class GeneratedMethods");
		code.AppendLine("{");

		var isFirst = true;

		foreach (var valueGroup in group.GroupBy(m => m.Value))
		{
			if (!isFirst)
			{
				code.AppendLine();
			}

			isFirst = false;

			// Add interceptor attributes
			foreach (var item in valueGroup)
			{
				code.AppendLine($"\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
			}

			// Generate the method implementation
			var first = valueGroup.First();

			var method = first.Method
				.WithIdentifier(SyntaxFactory.Identifier($"{first.Method.Identifier}_{first.Invocation.GetHashCode()}"))
				.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

			if (compilation.IsInterface(first.Method.ReturnType))
			{
				if (IsIEnumerable(compilation, first.Method.ReturnType) && first.Value is IEnumerable enumerable)
				{
					var block = SyntaxFactory.Block(enumerable
						.Cast<object?>()
						.Select(s => SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, CreateLiteral(s)).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))));

					method = method
						.WithBody(block)
						.WithExpressionBody(null);
				}
				else
				{
					var name = first.Method.ReturnType is GenericNameSyntax genericName
						? genericName.Identifier.Text
						: first.Method.ReturnType.ToString();

					var body = SyntaxFactory.Block(
						SyntaxFactory.ReturnStatement(
							SyntaxFactory.MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								SyntaxFactory.ParseTypeName($"{name}_{first.Value?.GetHashCode()}"),
								SyntaxFactory.IdentifierName("Instance"))));

					method = method
						.WithBody(body)
						.WithExpressionBody(null);
				}
			}
			else if (TryGetLiteral(first.Value, out var literal))
			{
				method = method
					.WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(literal)))
					.WithExpressionBody(null);
			}

			var methodCode = method
				.NormalizeWhitespace("\t")
				.ToString()
				.Replace("\n", "\n\t");

			code.AppendLine("\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			code.AppendLine(methodCode.Insert(0, "\t"));
		}

		code.AppendLine("}");

		foreach (var invocation in group.Distinct())
		{
			if (compilation.IsInterface(invocation.Method.ReturnType) && !IsIEnumerable(compilation, invocation.Method.ReturnType))
			{
				var returnType = compilation.GetSemanticModel(invocation.Method.SyntaxTree).GetTypeInfo(invocation.Method.ReturnType).Type;

				if (returnType is not INamedTypeSymbol namedTypeSymbol)
				{
					continue;
				}

				var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
				var elementName = elementType?.ToDisplayString();
				var hashCode = invocation.Value?.GetHashCode();

				IEnumerable<string> interfaces = [$"{namedTypeSymbol.Name}<{elementName}>"];

				code.AppendLine();

				using (code.AppendBlock($"file sealed class {namedTypeSymbol.Name}_{hashCode} : {String.Join(", ", interfaces)}"))
				{
					code.AppendLine($"public static {namedTypeSymbol.Name}_{hashCode} Instance = new {namedTypeSymbol.Name}_{hashCode}();");

					if (invocation.Value is IEnumerable enumerable)
					{
						code.AppendLine();
						code.AppendLine($"public static ReadOnlySpan<{elementName}> {namedTypeSymbol.Name}_{hashCode}_Data => [{String.Join(", ", (enumerable.Cast<object?>()).Select(CreateLiteral))}];");

						if (elementType is not null)
						{
							var items = enumerable.Cast<object?>().ToList();

							var interfaceBuilder = new InterfaceBuilder(compilation, loader, elementType, hashCode.GetValueOrDefault());
							var linqBuilder = new LinqBuilder(compilation, elementType, invocation.GenerationLevel, hashCode.GetValueOrDefault());
							var spanBuilder = new SpanBuilder(compilation, loader, elementType, invocation.GenerationLevel, hashCode.GetValueOrDefault());

							interfaceBuilder.AppendCount(namedTypeSymbol, items.Count, code);
							interfaceBuilder.AppendLength(namedTypeSymbol, items.Count, code);
							interfaceBuilder.AppendIsReadOnly(namedTypeSymbol, code);
							interfaceBuilder.AppendIndexer(namedTypeSymbol, items, code);
							interfaceBuilder.AppendAdd(namedTypeSymbol, code);
							interfaceBuilder.AppendClear(namedTypeSymbol, code);
							interfaceBuilder.AppendRemove(namedTypeSymbol, code);
							interfaceBuilder.AppendRemoveAt(namedTypeSymbol, code);
							interfaceBuilder.AppendInsert(namedTypeSymbol, code);
							interfaceBuilder.AppendIndexOf(namedTypeSymbol, items, code);
							interfaceBuilder.AppendCopyTo(namedTypeSymbol, items, code);
							interfaceBuilder.AppendContains(namedTypeSymbol, items, code);

							linqBuilder.AppendAll(namedTypeSymbol, items, code);
							linqBuilder.AppendAggregate(namedTypeSymbol, items, code);
							linqBuilder.AppendAny(namedTypeSymbol, items, code);
							linqBuilder.AppendAverage(namedTypeSymbol, items, code);
							linqBuilder.AppendCount(namedTypeSymbol, items, code);
							linqBuilder.AppendDistinct(namedTypeSymbol, items, code);
							linqBuilder.AppendElementAt(namedTypeSymbol, items, code);
							linqBuilder.AppendElementAtOrDefault(namedTypeSymbol, items, code);
							linqBuilder.AppendFirst(namedTypeSymbol, items, code);
							linqBuilder.AppendFirstOrDefault(namedTypeSymbol, items, code);
							linqBuilder.AppendLast(namedTypeSymbol, items, code);
							linqBuilder.AppendLastOrDefault(namedTypeSymbol, items, code);
							linqBuilder.AppendOrder(namedTypeSymbol, items, code);
							linqBuilder.AppendOrderDescending(namedTypeSymbol, items, code);
							linqBuilder.AppendSelect(namedTypeSymbol, items, code);
							linqBuilder.AppendSequenceEqual(namedTypeSymbol, items, code);
							linqBuilder.AppendSingle(namedTypeSymbol, items, code);
							linqBuilder.AppendSingleOrDefault(namedTypeSymbol, items, code);
							linqBuilder.AppendSum(namedTypeSymbol, items, code);
							linqBuilder.AppendWhere(namedTypeSymbol, items, code);

							linqBuilder.AppendToArray(namedTypeSymbol, items, code);
							linqBuilder.AppendToImmutableArray(namedTypeSymbol, items, code);
							linqBuilder.AppendToList(namedTypeSymbol, items, code);
							linqBuilder.AppendImmutableList(namedTypeSymbol, items, code);

							spanBuilder.AppendCommonPrefixLength(namedTypeSymbol, items, code);
							spanBuilder.AppendContainsAny(namedTypeSymbol, items, code);

							if (IsIEnumerableRecursive(namedTypeSymbol))
							{
								code.AppendLine();

								using (code.AppendBlock($"public IEnumerator<{elementName}> GetEnumerator()"))
								{
									foreach (var item in items)
									{
										code.AppendLine($"yield return {CreateLiteral(item)};");
									}
								}

								code.AppendLine();

								using (code.AppendBlock("IEnumerator IEnumerable.GetEnumerator()"))
								{
									code.AppendLine("return GetEnumerator();");
								}
							}
						}
					}

				}
			}
		}

		if (group.Key.Parent is TypeDeclarationSyntax type && !group.Any(a => a.Exceptions.Any()))
		{
			spc.AddSource($"{type.Identifier}_{group.Key.Identifier}.g.cs", code.ToString());
		}
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation
				|| !TryGetSymbol(context.SemanticModel, invocation, token, out var method))
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
				.Select(s => (GenerationLevel)s.Value.Value)
				.DefaultIfEmpty(GenerationLevel.Balanced)
				.FirstOrDefault();

			return GenerateExpression(context.SemanticModel.Compilation, loader, invocation, method, level, token);
		}

		return null;
	}

	private InvocationModel? GenerateExpression(Compilation compilation, MetadataLoader loader, InvocationExpressionSyntax invocation,
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

		var variables = ProcessArguments(compilation, loader, invocation, methodSymbol, token);

		if (variables == null)
		{
			return null;
		}

		if (TryGetOperation<IMethodBodyOperation>(compilation, methodDecl, out var blockOperation) &&
				compilation.TryGetSemanticModel(invocation, out var model))
		{
			try
			{
				var timer = Stopwatch.StartNew();
				var exceptions = new ConcurrentDictionary<SyntaxNode, Exception>(SyntaxNodeComparer<SyntaxNode>.Instance);

				var visitor = new ConstExprOperationVisitor(compilation, loader, (operation, ex) =>
				{
					exceptions.TryAdd(operation!.Syntax, ex);
				}, token);

				visitor.VisitBlock(blockOperation.BlockBody!, variables);
				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				return new InvocationModel
				{
					Usings = GetUsings(methodSymbol),
					Method = methodDecl,
					Invocation = invocation,
					Value = variables[ConstExprOperationVisitor.ReturnVariableName],
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

	private Dictionary<string, object?>? ProcessArguments(Compilation compilation, MetadataLoader loader, InvocationExpressionSyntax invocation,
																												IMethodSymbol methodSymbol, CancellationToken token)
	{
		var variables = new Dictionary<string, object?>();

		for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			var paramName = methodSymbol.Parameters[i].Name;

			if (methodSymbol.Parameters[i].IsParams)
			{
				var values = invocation.ArgumentList.Arguments
					.Skip(i)
					.Select(arg => GetConstantValue(compilation, loader, arg.Expression, token))
					.ToArray();

				if (methodSymbol.Parameters[i].IsParamsArray)
				{
					var array = Array.CreateInstance(values[0].GetType(), values.Length);

					for (var j = 0; j < values.Length; j++)
					{
						array.SetValue(values[j], j);
					}
					variables[paramName] = array;
				}
				else
				{
					var listType = typeof(List<>).MakeGenericType(values[0].GetType());
					var list = Activator.CreateInstance(listType);

					if (list is IList listInstance)
					{
						foreach (var item in values)
						{
							listInstance.Add(item);
						}
					}
					variables[paramName] = list;
				}
				break;
			}

			var arg = invocation.ArgumentList.Arguments[i];

			if (!TryGetConstantValue(compilation, loader, arg.Expression, token, out var value))
			{
				return null;
			}
			variables[paramName] = value;
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

	private static HashSet<string> GetUsings(IMethodSymbol methodSymbol)
	{
		var usings = new HashSet<string>
		{
			"using System.Diagnostics;",
			"using System.Runtime.CompilerServices;",
			"using System.Collections.Generic;",
			"using System.Collections;",
			"using System;",
			"using System.Linq;",
			"using System.Diagnostics.CodeAnalysis;",
			"using System.Runtime.Intrinsics;",
			"using System.Numerics;",
			"using System.Runtime.InteropServices;",
			$"using {methodSymbol.ReturnType.ContainingNamespace};"
		};

		foreach (var p in methodSymbol.Parameters)
		{
			usings.Add($"using {p.Type.ContainingNamespace};");
		}

		return usings;
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

		var descriptor = new DiagnosticDescriptor(
			"CEA005",
			"Exception during evaluation",
			"Unable to evaluate: {0}",
			"Usage",
			DiagnosticSeverity.Warning,
			true);

		foreach (var exception in exceptions)
		{
			spc.ReportDiagnostic(Diagnostic.Create(descriptor, exception.GetLocation(), exception));
		}
	}
}
#pragma warning restore RSEXPERIMENTAL002