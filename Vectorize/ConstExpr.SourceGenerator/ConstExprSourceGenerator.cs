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
using System.Collections.Immutable;
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
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel?> group)
	{
		var code = new IndentedStringBuilder();
		var usings = group.SelectMany(item => item.Usings).Distinct().OrderBy(s => s);

		IndentedStringBuilder.Compilation = compilation;

		using var loader = MetadataLoader.GetLoader(compilation);

		foreach (var u in usings.Where(w => !String.IsNullOrWhiteSpace(w)))
		{
			code.AppendLine($"using {(LiteralString) u};");
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
				code.AppendLine((string)$"\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
			}

			// Generate the method implementation
			var first = valueGroup.First();

			var method = first.Method
				.WithIdentifier(SyntaxFactory.Identifier($"{first.Method.Identifier}_{Math.Abs(first.Value?.GetHashCode() ?? 0)}"))
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
								SyntaxFactory.ParseTypeName($"{name}_{Math.Abs(first.Value?.GetHashCode() ?? 0)}")
									.WithTrailingTrivia(SyntaxFactory.Space),
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

			// code.AppendLine("\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			code.AppendLine(methodCode.Insert(0, "\t"));
		}

		code.AppendLine("}");

		foreach (var invocation in group.DistintBy(d => d.Value))
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
				var elementName = compilation.GetMinimalString(elementType);
				var dataName = $"{namedTypeSymbol.Name}_{hashCode}_Data";

				IEnumerable<string> interfaces = [compilation.GetMinimalString(returnType)];

				code.AppendLine();

				using (code.AppendBlock((string)$"file sealed class {namedTypeSymbol.Name}_{hashCode} : {String.Join(", ", interfaces)}"))
				{
					code.AppendLine((string)$"public static {namedTypeSymbol.Name}_{hashCode} Instance = new {namedTypeSymbol.Name}_{hashCode}();");

					if (invocation.Value is IEnumerable enumerable)
					{
						elementType ??= enumerable
							.Cast<object?>()
							.Where(w => w is not null)
							.Select(s => compilation.GetTypeByType(s.GetType()))
							.First();

						elementName ??= compilation.GetMinimalString(elementType);

						code.AppendLine();

						if (compilation.IsSpecialType(elementType, SpecialType.System_Char))
						{
							code.AppendLine((string)$"public static ReadOnlySpan<{elementName}> {dataName} => \"{String.Join(String.Empty, enumerable.Cast<object?>())}\";");
						}
						else
						{
							code.AppendLine((string)$"public static ReadOnlySpan<{elementName}> {dataName} => [{String.Join(", ", (enumerable.Cast<object?>()).Select(CreateLiteral))}];");
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
								switch (member)
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
												 || memoryExtensionsBuilder.AppendBinarySearch(method, items, code)
												 // || memoryExtensionsBuilder.AppendCommonPrefixLength(method, items, code)
												 // || memoryExtensionsBuilder.AppendContainsAny(method, items, code)
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
								code.AppendLine();

								using (code.AppendBlock($"public IEnumerator<{(LiteralString)elementName}> GetEnumerator()"))
								{
									using (code.AppendBlock($"for (var i = 0; i < {(LiteralString) dataName}.Length; i++)"))
									{
										code.AppendLine($"yield return {(LiteralString)dataName}[i];");
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
				var usings = new HashSet<string?>
				{
					"System.Runtime.CompilerServices",
					"System",
					"System.Linq",
				};

				var visitor = new ConstExprOperationVisitor(compilation, loader, (operation, ex) =>
				{
					exceptions.TryAdd(operation!.Syntax, ex);
				}, token);

				visitor.VisitBlock(blockOperation.BlockBody!, variables);
				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				GetUsings(methodSymbol, BaseBuilder.IsPerformance(level, (variables[ConstExprOperationVisitor.ReturnVariableName] as IEnumerable)?.Cast<object?>()?.Count() ?? 0), usings);

				return new InvocationModel
				{
					Usings = usings,
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

	private static void GetUsings(IMethodSymbol methodSymbol, bool isPerformance, HashSet<string?> usings)
	{
		if (isPerformance)
		{
			usings.Add("System.Numerics");
			usings.Add("System.Collections");
			usings.Add("System.Runtime.InteropServices");
		}

		usings.Add(methodSymbol.ReturnType.ContainingNamespace?.ToString());

		if (methodSymbol.ReturnType is INamedTypeSymbol namedTypeSymbol)
		{
			foreach (var type in namedTypeSymbol.TypeArguments)
			{
				usings.Add(type.ContainingNamespace?.ToString());
			}
		}

		foreach (var p in methodSymbol.Parameters)
		{
			usings.Add(p.Type.ContainingNamespace?.ToString());
		}

		foreach (var type in methodSymbol.TypeParameters.SelectMany(s => s.ConstraintTypes))
		{
			usings.Add(type.ContainingNamespace?.ToString());
		}

		if (!IsIEnumerable(methodSymbol.ReturnType) && methodSymbol.ReturnType.TypeKind == TypeKind.Interface)
		{
			usings.Add("System.Runtime.Intrinsics");

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