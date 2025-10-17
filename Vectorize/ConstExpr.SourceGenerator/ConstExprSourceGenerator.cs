using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
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

				foreach (var methodGroup in modelAndCompilation.Left.Left.GroupBy(m => m.OriginalMethod, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
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

		var distinctAdditionalMethods = methodGroup
			.SelectMany(m => m?.AdditionalMethods)
			.Distinct(SyntaxNodeComparer<SyntaxNode>.Instance);

		//code.WriteLine();

		using (code.WriteBlock($"namespace ConstantExpression.Generated", "{", "}"))
		{
			// Emit top-level generated methods grouped by value.
			using (code.WriteBlock($"file static class GeneratedMethods", "{", "}"))
			{
				EmitGeneratedMethodsForValueGroups(code, compilation, methodGroup);

				foreach (var additionalMethod in distinctAdditionalMethods)
				{
					code.WriteLine();
					code.WriteLine(additionalMethod.ToString(), true);
				}
			}

			// Emit concrete interface implementations (non IEnumerable interfaces) per distinct value.
			// EmitInterfaceImplementations(code, compilation, methodGroup, loader, distinctUsings);
		}

		EmitInterceptsLocationAttributeStub(code);

		// if (!methodGroup.SelectMany(s => s!.Exceptions).Any())
		// {
			var result = String.Join("\n", distinctUsings
				.Where(w => !String.IsNullOrWhiteSpace(w))
				.OrderByDescending(o => o.StartsWith("System"))
				.ThenBy(o => o)
				.Select(s => $"using {s};")) + "\n\n" + code;

			spc.AddSource($"{methodGroup.First().ParentType.Identifier}_{methodGroup.Key.Identifier}.g.cs", result);
		// }
	}

	#region Emission Helpers

	private void EmitGeneratedMethodsForValueGroups(IndentedCodeWriter code, Compilation compilation, IEnumerable<InvocationModel?> methodGroup)
	{
		var wroteFirstGroup = false;

		foreach (var invocationsByValue in methodGroup.Where(w => w?.Location is not null).GroupBy(m => m.Method.Identifier.ValueText, StringComparer.CurrentCultureIgnoreCase))
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

			code.WriteLine(invocationsByValue.First().Method.ToFullString(), true);
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
			var data = attribute.ToAttribute<ConstExprAttribute>(loader);

			return GenerateExpression(context, loader, invocation, methodSymbol, data, token);
		}

		return null;
	}

	private InvocationModel? GenerateExpression(GeneratorSyntaxContext context, MetadataLoader loader, InvocationExpressionSyntax invocation,
																							IMethodSymbol methodSymbol, ConstExprAttribute attribute, CancellationToken token)
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
					context.SemanticModel.Compilation.TryGetSemanticModel(methodDecl, out var model))
			{
				var usings = new HashSet<string?>
				{
					"System.Runtime.CompilerServices",
					"System",
				};
				
				// var variables = ProcessArguments(visitor, context.SemanticModel.Compilation, invocation, loader, token);
				var variablesPartial = ProcessArguments(visitor, context.SemanticModel, invocation, loader, token);
				var additionalMethods = new Dictionary<SyntaxNode, bool>(SyntaxNodeComparer<SyntaxNode>.Instance);

				var partialVisitor = new ConstExprPartialRewriter(model, loader, (node, ex) =>
				{
					exceptions.TryAdd(node, ex);
				}, variablesPartial, additionalMethods, usings, attribute, token);

				

				var timer = Stopwatch.StartNew();

				// visitor.VisitBlock(blockOperation.BlockBody!, variables);

				var result = partialVisitor.VisitBlock(methodDecl.Body); // partialVisitor.VisitBlock(blockOperation.BlockBody!, variablesPartial);
				var result2 = new PruneVariableRewriter(model, loader, variablesPartial).Visit(result)!;

				// Format using Roslyn formatter instead of NormalizeWhitespace
				// var text = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result));
				// var text2 = FormattingHelper.Render(methodDecl.WithBody((BlockSyntax)result2));

				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				GetUsings(methodSymbol, usings);

				return new InvocationModel
				{
					Usings = usings!,
					OriginalMethod = methodDecl,
					Method = FormattingHelper.Format(methodDecl
						.WithIdentifier(SyntaxFactory.Identifier($"{methodDecl.Identifier.Text}_{result2.GetDeterministicHash()}")
							.WithLeadingTrivia(methodDecl.Identifier.LeadingTrivia)
							.WithTrailingTrivia(methodDecl.Identifier.TrailingTrivia))
						.WithBody((BlockSyntax)result2)) as MethodDeclarationSyntax ?? methodDecl,
					AdditionalMethods = additionalMethods
						.OrderByDescending(o => o.Value)
						.Select(s => FormattingHelper.Format(s.Key)),
					ParentType = methodDecl.Parent as TypeDeclarationSyntax,
					Invocation = invocation,
					Location = context.SemanticModel.GetInterceptableLocation(invocation, token),
					Exceptions = exceptions!,
				};
			}
		}
		catch (Exception e)
		{
			Logger.Error(e, $"Error processing {invocation}: {e.Message}");
		}

		return null;
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
			variables.Add($"#{parameter.Name}", new VariableItem(argument, true, loader.GetType(argument), true));
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
}

#pragma warning restore RSEXPERIMENTAL002