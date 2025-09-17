using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Builders;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Rewriters;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SGF;
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
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

[assembly: InternalsVisibleTo("ConstExpr.Tests")]

namespace ConstExpr.SourceGenerator;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public class ConstExprSourceGenerator() : IncrementalGenerator("ConstExpr")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		var invocations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: (node, token) => !token.IsCancellationRequested && node is InvocationExpressionSyntax,
				transform: GenerateSource)
			.Where(result => result != null);

		var rootNamespace = context
			.AnalyzerConfigOptionsProvider
			.Select((c, _) =>
				c.GlobalOptions.TryGetValue("build_property.UseConstExpr", out var enableSwitch)
				&& enableSwitch.Equals("true", StringComparison.Ordinal));

		context.RegisterSourceOutput(invocations.Collect().Combine(context.CompilationProvider).Combine(rootNamespace), (spc, modelAndCompilation) =>
		{
			if (modelAndCompilation.Right)
			{
				var loader = MetadataLoader.GetLoader(modelAndCompilation.Left.Right);

				foreach (var methodGroup in modelAndCompilation.Left.Left.GroupBy(m => m.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
				{
					try
					{
						GenerateMethodImplementations(spc, modelAndCompilation.Left.Right, methodGroup, loader);
					}
					catch (Exception ex)
					{
						Logger.Error(ex, $"Error generating implementations for {methodGroup.Key.Identifier}: {ex.Message}");
					}
				}

				ReportExceptions(spc, modelAndCompilation.Left.Left);
			}
		});
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel?> methodGroup, MetadataLoader loader)
	{
		var code = new IndentedCodeWriter(compilation);

		var distinctUsings = methodGroup
			.SelectMany(m => m?.Usings ?? [])
			.ToSet();

		//code.WriteLine();

		using (code.WriteBlock($"namespace ConstantExpression.Generated", "{", "}"))
		{
			// Emit top-level generated methods grouped by value.
			using (code.WriteBlock($"file static class GeneratedMethods", "{", "}"))
			{
				EmitGeneratedMethodsForValueGroups(code, compilation, methodGroup);
			}

			// Emit concrete interface implementations (non IEnumerable interfaces) per distinct value.
			EmitInterfaceImplementations(code, compilation, methodGroup, loader, distinctUsings);
		}

		EmitInterceptsLocationAttributeStub(code);

		if (methodGroup.Key.Parent is TypeDeclarationSyntax declaringType && !methodGroup.SelectMany(s => s!.Exceptions).Any())
		{
			var result = String.Join("\n", distinctUsings
				.Where(w => !String.IsNullOrWhiteSpace(w))
				.OrderByDescending(o => o.StartsWith("System"))
				.ThenBy(o => o)
				.Select(s => $"using {s};")) + "\n\n" + code;

			// spc.AddSource($"{declaringType.Identifier}_{methodGroup.Key.Identifier}.g.cs", result);
		}
	}

	#region Emission Helpers

	private void EmitGeneratedMethodsForValueGroups(IndentedCodeWriter code, Compilation compilation, IEnumerable<InvocationModel?> methodGroup)
	{
		var wroteFirstGroup = false;

		foreach (var invocationsByValue in methodGroup.Where(w => w?.Location is not null).GroupBy(m => m.Value, ValueOrCollectionEqualityComparer<object?>.Instance))
		{
			if (wroteFirstGroup)
			{
				code.WriteLine();
			}

			wroteFirstGroup = true;

			// Add interceptor attributes for every invocation (location based) that shares the same value.
			foreach (var invocationModel in invocationsByValue)
			{
				code.WriteLine($"[InterceptsLocation({invocationModel.Location.Version}, {invocationModel.Location.Data})]");
			}

			var representativeInvocation = invocationsByValue.First();
			var originalMethodSyntax = representativeInvocation.Method;
			var valueHashSuffix = GetValueHashSuffix(representativeInvocation.Value);

			var generatedMethodSyntax = originalMethodSyntax
				.WithIdentifier(SyntaxFactory.Identifier($"{originalMethodSyntax.Identifier}_{valueHashSuffix}"));

			// Adjust return type for array cases where we can use the specific runtime array type name.
			generatedMethodSyntax = AdjustArrayReturnTypeIfNeeded(generatedMethodSyntax, representativeInvocation.Value, representativeInvocation.Symbol.ReturnType);

			using (code.WriteBlock(generatedMethodSyntax))
			{
				if (compilation.IsInterface(originalMethodSyntax.ReturnType))
				{
					EmitInterfaceReturnBody(code, compilation, representativeInvocation, representativeInvocation.Value);
				}
				else if (TryGetLiteral(representativeInvocation.Value, out var literal))
				{
					code.WriteLine($"return {literal};");
				}
			}
		}
	}

	private void EmitInterfaceImplementations(IndentedCodeWriter code, Compilation compilation, IEnumerable<InvocationModel?> methodGroup, MetadataLoader loader, ISet<string> usings)
	{
		// Only unique values.
		foreach (var invocationModel in methodGroup.DistinctBy(d => d.Value))
		{
			var valueHashSuffix = GetValueHashSuffix(invocationModel.Value);

			if (compilation.IsInterface(invocationModel.Method.ReturnType) && !IsIEnumerable(compilation, invocationModel.Method.ReturnType))
			{
				if (!TryGetInterfaceSymbol(compilation, invocationModel, out var namedTypeSymbol))
				{
					continue;
				}

				var elementType = namedTypeSymbol.TypeArguments.FirstOrDefault();
				var dataFieldName = $"{namedTypeSymbol.Name}_{valueHashSuffix}_Data";
				IEnumerable<string> interfaces = [compilation.GetMinimalString(namedTypeSymbol)];

				code.WriteLine();

				using (code.WriteBlock($"file sealed class {namedTypeSymbol.Name:literal}_{valueHashSuffix:literal} : {String.Join(", ", interfaces):literal}"))
				{
					code.WriteLine($"public static {namedTypeSymbol.Name:literal}_{valueHashSuffix:literal} Instance = new {namedTypeSymbol.Name:literal}_{valueHashSuffix:literal}();");

					if (invocationModel.Value is IEnumerable enumerable)
					{
						var resolvedElementType = elementType ?? enumerable
							.Cast<object?>()
							.Where(w => w is not null)
							.Select(s => compilation.GetTypeByType(s.GetType()))
							.First();

						EmitCollectionBackingField(code, invocationModel, resolvedElementType, dataFieldName, enumerable, usings);
						EmitInterfaceMemberImplementations(code, compilation, loader, invocationModel, namedTypeSymbol, resolvedElementType, dataFieldName, enumerable, usings);
					}
				}
			}
		}
	}

	private static void EmitCollectionBackingField(IndentedCodeWriter code, InvocationModel invocationModel, ITypeSymbol elementType, string dataFieldName, IEnumerable enumerable, ISet<string> usings)
	{
		code.WriteLine();

		if (elementType is { } && invocationModel.Value is IEnumerable)
		{
			if (elementType.SpecialType == SpecialType.System_Char)
			{
				code.WriteLine($"public static ReadOnlySpan<{elementType}> {dataFieldName:literal} => \"{String.Join(String.Empty, enumerable.Cast<object?>()):literal}\";");

				usings.Add("System");
			}
			else if (elementType.IsVectorSupported())
			{
				code.WriteLine($"public static ReadOnlySpan<{elementType}> {dataFieldName:literal} => [{enumerable}];");

				usings.Add("System");
			}
			else
			{
				code.WriteLine($"public static {elementType}[] {dataFieldName:literal} = [{enumerable}];");
			}
		}
	}

	private void EmitInterfaceMemberImplementations(IndentedCodeWriter code, Compilation compilation, MetadataLoader loader, InvocationModel invocationModel, INamedTypeSymbol interfaceType, ITypeSymbol elementType, string dataFieldName, IEnumerable enumerable, ISet<string> usings)
	{
		var items = enumerable
			.Cast<object?>()
			.ToImmutableArray();

		var members = interfaceType.AllInterfaces
			.Prepend(interfaceType)
			.SelectMany(s => s.GetMembers())
			.Distinct(SymbolEqualityComparer.Default)
			.OrderBy(o => o is not IPropertySymbol);

		var interfaceBuilder = new InterfaceBuilder(compilation, loader, elementType, invocationModel.GenerationLevel, dataFieldName, usings);
		var enumerableBuilder = new EnumerableBuilder(compilation, elementType, loader, invocationModel.GenerationLevel, dataFieldName, usings);
		var memoryExtensionsBuilder = new MemoryExtensionsBuilder(compilation, loader, elementType, invocationModel.GenerationLevel, dataFieldName, usings);

		foreach (var member in members)
		{
			if (TryWrite(code, member, items, enumerable, interfaceBuilder, enumerableBuilder, memoryExtensionsBuilder))
			{
				switch (member)
				{
					case IMethodSymbol methodSymbol:
						GetUsings(methodSymbol, usings);
						break;
					case IPropertySymbol propertySymbol:
						SetUsings(propertySymbol.Type, usings);

						foreach (var parameter in propertySymbol.Parameters)
						{
							SetUsings(parameter.Type, usings);
						}
						break;
				}

				continue;
			}

			if (!IsIEnumerableRecursive(interfaceType))
			{
				var descriptor = new DiagnosticDescriptor(
					"CEA006",
					"Unable to implement {0}",
					"Unable to implement {0}",
					"Usage",
					DiagnosticSeverity.Error,
					true);

				// We can't access SgfSourceProductionContext here; original behavior only reported when generation finished.
				// Intentionally left as originally designed – would need redesign to surface diagnostics here.
			}
		}

		if (IsIEnumerableRecursive(interfaceType))
		{
			usings.Add("System.Collections");
			usings.Add("System.Collections.Generic");

			code.WriteLine();

			// IEnumerator<T> implementation
			using (code.WriteBlock($"public IEnumerator<{elementType}> GetEnumerator()"))
			{
				if (items.IsEmpty)
				{
					code.WriteLine($"return Enumerable.Empty<{elementType}>().GetEnumerator();");
				}
				else if (elementType.IsVectorSupported())
				{
					using (code.WriteBlock($"for (var i = 0; i < {dataFieldName:literal}.Length; i++"))
					{
						code.WriteLine($"yield return {dataFieldName:literal}[i];");
					}
				}
				else
				{
					code.WriteLine($"return Array.AsReadOnly({dataFieldName:literal}).GetEnumerator();");
				}
			}

			code.WriteLine();

			using (code.WriteBlock($"IEnumerator IEnumerable.GetEnumerator()", "{", "}"))
			{
				code.WriteLine("return GetEnumerator();");
			}
		}
	}

	private static MethodDeclarationSyntax AdjustArrayReturnTypeIfNeeded(MethodDeclarationSyntax methodSyntax, object? value, ITypeSymbol returnTypeSymbol)
	{
		if (returnTypeSymbol is IArrayTypeSymbol arraySymbol && value is Array runtimeArray)
		{
			var runtimeElementType = runtimeArray.GetType().GetElementType();

			if (runtimeElementType?.FullName == arraySymbol.ElementType.ToDisplayString() && runtimeArray.Rank == arraySymbol.Rank)
			{
				methodSyntax = methodSyntax.WithReturnType(
					SyntaxFactory.ParseTypeName(runtimeArray.GetType().Name)
						.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
			}
		}
		return methodSyntax;
	}

	private static void EmitInterfaceReturnBody(IndentedCodeWriter code, Compilation compilation, InvocationModel representativeInvocation, object? value)
	{
		// IEnumerable optimized emission.
		if ((IsIEnumerable(compilation, representativeInvocation.Method.ReturnType) || IsIAsyncEnumerable(compilation, representativeInvocation.Method.ReturnType)) && value is IEnumerable enumerable)
		{
			if (compilation.GetSemanticModel(representativeInvocation.Method.SyntaxTree).GetTypeInfo(representativeInvocation.Method.ReturnType).Type is not INamedTypeSymbol returnTypeInfo)
			{
				return;
			}

			if (compilation.TryGetIEnumerableType(returnTypeInfo, true, out var tempElementType) && tempElementType is INamedTypeSymbol resolvedElementType)
			{
				returnTypeInfo = resolvedElementType;
			}

			var data = enumerable.Cast<object?>().ToArray();

			if (IsIAsyncEnumerable(returnTypeInfo))
			{
				if (returnTypeInfo.IsVectorSupported() && data.IsSequenceDifference(out var difference))
				{
					if (ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, difference, 0.ToSpecialType(returnTypeInfo.SpecialType)) is true)
					{
						code.WriteLine($$"""
							var start = {{data[0]}};

							do
							{
								yield return start;
								start -= {{difference.Abs(returnTypeInfo.SpecialType)}};
							}
							while (start >= {{data[^1]}});
							""");
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
						code.WriteLine($"yield return {CreateLiteral(item)};");
					}
				}

			}
			else
			{
				if (data.Length == 1)
				{
					code.WriteLine($"return [{data[0]}];");
				}
				else if (data.IsSame(data.FirstOrDefault()))
				{
					code.WriteLine($"return Enumerable.Repeat({CreateLiteral(data.FirstOrDefault())}, {CreateLiteral(data.Length)});");
				}
				else if (data.IsNumericSequence() && compilation.IsSpecialType(returnTypeInfo, SpecialType.System_Int32))
				{
					code.WriteLine($"return Enumerable.Range({CreateLiteral(data[0])}, {CreateLiteral(data.Length)});");
				}
				else if (returnTypeInfo.IsVectorSupported() && data.IsSequenceDifference(out var difference))
				{
					if (compilation.GetTypeByName(typeof(Enumerable).FullName).HasMethod("Sequence"))
					{
						code.WriteLine($"return Enumerable.Sequence({CreateLiteral(data[0])}, {CreateLiteral(data[^1])}, {CreateLiteral(difference)});");
					}
					else
					{
						if (ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, difference, 0.ToSpecialType(returnTypeInfo.SpecialType)) is true)
						{
							code.WriteLine($$"""
								var start = {{data[0]}};

								do
								{
									yield return start;
									start -= {{difference.Abs(returnTypeInfo.SpecialType)}};
								}
								while (start >= {{data[^1]}});
								""");
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
				}
				else
				{
					foreach (var item in data)
					{
						code.WriteLine($"yield return {CreateLiteral(item)};");
					}
				}
			}
		}
		else
		{
			var returnTypeName = representativeInvocation.Method.ReturnType is GenericNameSyntax g ? g.Identifier.Text : representativeInvocation.Method.ReturnType.ToString();
			code.WriteLine($"return {returnTypeName:literal}_{GetValueHashSuffix(value):literal}.Instance;");
		}
	}

	private static void EmitInterceptsLocationAttributeStub(IndentedCodeWriter code)
	{
		code.WriteLine("""

			namespace System.Runtime.CompilerServices
			{
				[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
				file sealed class InterceptsLocationAttribute : Attribute
				{
					public InterceptsLocationAttribute(int version, string data)
					{
						_ = version;
						_ = data;
					}
				}
			}
			""");
	}

	#endregion

	private static string GetValueHashSuffix(object? value)
	{
		var hash = ValueOrCollectionEqualityComparer<object?>.Instance.GetHashCode(value);
		var bytes = BitConverter.GetBytes(hash);
		var b64 = Convert.ToBase64String(bytes);

		// Sanitize to identifier-safe characters and ensure it doesn't start with a digit
		b64 = b64.Replace('+', '_').Replace('/', '_').Replace("=", String.Empty);

		return b64;

		// Build a content-aware hash for collections; otherwise use value.GetHashCode()
		int ComputeContentHash(object? obj)
		{
			if (obj is null)
			{
				return 0;
			}

			// Strings as scalar values
			if (obj is string s)
			{
				return s.GetHashCode();
			}

			// Collections: fold element hashes recursively
			if (obj is IEnumerable enumerable)
			{
				unchecked
				{
					var hash = 19;

					foreach (var item in enumerable)
					{
						hash = hash * 31 + ComputeContentHash(item);
					}

					return hash;
				}
			}

			// Fallback: object hash
			return obj.GetHashCode();
		}
	}

	private static bool TryGetInterfaceSymbol(Compilation compilation, InvocationModel invocationModel, out INamedTypeSymbol? namedTypeSymbol)
	{
		var returnType = compilation.GetSemanticModel(invocationModel.Method.SyntaxTree).GetTypeInfo(invocationModel.Method.ReturnType).Type;
		namedTypeSymbol = returnType as INamedTypeSymbol;
		return namedTypeSymbol != null;
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation
				|| !TryGetSymbol(context.SemanticModel, invocation, token, out var methodSymbol)
				|| !methodSymbol.IsStatic)
		{
			return null;
		}

		var attribute = methodSymbol.GetAttributes().FirstOrDefault(IsConstExprAttribute)
										?? methodSymbol.ContainingType?.GetAttributes().FirstOrDefault(IsConstExprAttribute)
										?? methodSymbol.ContainingAssembly.GetAttributes().FirstOrDefault(IsConstExprAttribute);

		// Check for ConstExprAttribute on type or method
		if (attribute is not null && !IsInConstExprBody(context.SemanticModel.Compilation, invocation))
		{
			var loader = MetadataLoader.GetLoader(context.SemanticModel.Compilation);

			var level = attribute.NamedArguments
				.Where(w => w.Key == "Level")
				.Select(s => (GenerationLevel)s.Value.Value)
				.DefaultIfEmpty(GenerationLevel.Balanced)
				.FirstOrDefault();

			return GenerateExpression(context, loader, invocation, methodSymbol, level, token);
		}

		return null;
	}

	private InvocationModel? GenerateExpression(GeneratorSyntaxContext context, MetadataLoader loader, InvocationExpressionSyntax invocation,
																							IMethodSymbol methodSymbol, GenerationLevel level, CancellationToken token)
	{
		var methodDecl = GetMethodSyntaxNode(methodSymbol);

		if (methodDecl == null)
		{
			return null;
		}

		var exceptions = new ConcurrentDictionary<SyntaxNode?, Exception>(SyntaxNodeComparer<SyntaxNode>.Instance);

		var visitor = new ConstExprOperationVisitor(context.SemanticModel.Compilation, loader, (operation, ex) =>
		{
			// exceptions.TryAdd(operation!.Syntax, ex);
		}, token);

		try
		{
			if ( //exceptions.IsEmpty
					TryGetOperation<IMethodBodyOperation>(context.SemanticModel.Compilation, methodDecl, out var blockOperation)
					&& context.SemanticModel.Compilation.TryGetSemanticModel(methodDecl, out var model))
			{
				// var variables = ProcessArguments(visitor, context.SemanticModel.Compilation, invocation, loader, token);
				var variablesPartial = ProcessArguments(visitor, context.SemanticModel, invocation, loader, token);

				var partialVisitor = new ConstExprPartialRewriter(model, loader, (node, ex) =>
				{
					exceptions.TryAdd(node, ex);
				}, variablesPartial, token);

				var usings = new HashSet<string?>
				{
					"System.Runtime.CompilerServices",
					"System",
				};

				var timer = Stopwatch.StartNew();

				// visitor.VisitBlock(blockOperation.BlockBody!, variables);

				var result = partialVisitor.VisitBlock(methodDecl.Body); // partialVisitor.VisitBlock(blockOperation.BlockBody!, variablesPartial);
				var result2 = new PruneVariableRewriter(variablesPartial).Visit(result)!;

				// Format using Roslyn formatter instead of NormalizeWhitespace
				var text = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result));
				var text2 = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result2));

				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				GetUsings(methodSymbol, usings);

				//return new InvocationModel
				//{
				//	Usings = usings,
				//	Method = methodDecl,
				//	Symbol = methodSymbol,
				//	Invocation = invocation,
				//	Value = variables[ConstExprOperationVisitor.RETURNVARIABLENAME],
				//	Location = model.GetInterceptableLocation(invocation, token),
				//	Exceptions = exceptions,
				//	GenerationLevel = level,
				//};
			}
		}
		catch (Exception e)
		{
			Logger.Error(e, $"Error processing {invocation}: {e.Message}");
		}

		return new InvocationModel
		{
			Method = methodDecl,
			Symbol = methodSymbol,
			Invocation = invocation,
			// Location = model.GetInterceptableLocation(invocation, token),
			Exceptions = exceptions,
			GenerationLevel = level,
		};
	}

	public static Dictionary<string, VariableItem> ProcessArguments(ConstExprOperationVisitor visitor, SemanticModel model, InvocationExpressionSyntax invocation, MetadataLoader loader, CancellationToken token)
	{
		var variables = new Dictionary<string, VariableItem>();
		var invocationOperation = model.GetOperation(invocation) as IInvocationOperation;
		var methodSymbol = invocationOperation?.TargetMethod;

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
				try
				{
					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type ?? argument.Parameter.Type, true, visitor.Visit(argument.Value, new VariableItemDictionary(variables)), true));
				}
				catch (Exception)
				{
					variables.Add(argument.Parameter.Name, new VariableItem(argument.Type ?? argument.Parameter.Type, false, argument.Syntax, true));
				}
			}
		}

		foreach (var (parameter, argument) in methodSymbol.TypeParameters.Zip(methodSymbol.TypeArguments, (x, y) => (x, y)))
		{
			variables.Add(parameter.Name, new VariableItem(argument, true, loader.GetType(argument), true));
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

	private static void GetUsings(IMethodSymbol methodSymbol, ISet<string?> usings)
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
				SetUsings(arrayType.ElementType, usings);
				break;
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
			if (exceptions.Any(a => a != exception && exception.Span.Contains(a.Span)))
			{
				continue;
			}

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
						 || memoryExtensionsBuilder.AppendContainsAnyInRange(method, items, code)
						 || memoryExtensionsBuilder.AppendCount(method, items, code)
						 || memoryExtensionsBuilder.AppendEndsWith(method, items, code)
						 || memoryExtensionsBuilder.AppendEnumerateLines(method, enumerable as string, code)
						 || memoryExtensionsBuilder.AppendEnumerableRunes(method, enumerable as string, code)
						 || memoryExtensionsBuilder.AppendIsWhiteSpace(method, enumerable as string, code)
						 || memoryExtensionsBuilder.AppendReplace(method, items, code):
				return true;
			default:
				return false;
		}
	}
}

#pragma warning restore RSEXPERIMENTAL002