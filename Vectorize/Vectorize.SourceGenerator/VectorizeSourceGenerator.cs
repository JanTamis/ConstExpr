using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SGF;
using Vectorize.Rewriters;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Operations;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public class VectorizeSourceGenerator() : IncrementalGenerator("Vectorize")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(x =>
		{
			//var method = context.SyntaxProvider.ForAttributeWithMetadataName("VectorizeAttribute", (node, token) => !token.IsCancellationRequested, GenerateSource);

			var method = context.SyntaxProvider
				.CreateSyntaxProvider((node, token) => !token.IsCancellationRequested && node is InvocationExpressionSyntax, GenerateSource)
				.WithComparer(EqualityComparer<InvocationModel?>.Default)
				.Where(w => w is not null);

			context.RegisterSourceOutput(method.Collect(), static (spc, source) =>
			{
				var groups = source.GroupBy(g => g.Method, x => x, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance);
				
				foreach (var group in groups)
				{
					var builder = new StringBuilder();
					
					var usings = group
						.Where(w => w.Usings.Any())
						.SelectMany(s => s.Usings)
						.Distinct();

					foreach (var item in usings.OrderBy(o => o))
					{
						builder.AppendLine(item);
					}

					builder.AppendLine();

					var index = 1;

					builder.AppendLine("namespace ConstantExpression.Generated");
					builder.AppendLine("{");

					builder.AppendLine("\tfile static class GeneratedMethods");
					builder.Append("\t{");

					foreach (var item in group)
					{
						builder.AppendLine();

						builder.AppendLine($"\t\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
						builder.AppendLine("\t\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
						builder.AppendLine(item.Node
							.WithIdentifier(SyntaxFactory.Identifier($"{item.Node.Identifier}{index++}"))
							.ToString()
							.Replace("\n", "\n\t\t")
							.Insert(0, "\t\t"));
					}

					builder.AppendLine("\t}");
					builder.AppendLine("}");

					builder.AppendLine("""
						
						namespace System.Runtime.CompilerServices
						{
							[Conditional("DEBUG")]
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

					spc.AddSource($"{group.Key.Identifier}.g.cs", builder.ToString());
				}
			});
			
			x.AddSource("ConstExprAttribute.g", """
				using System;
				
				namespace ConstantExpression;

				[AttributeUsage(AttributeTargets.Method, Inherited = false)]
				public sealed class ConstExprAttribute : Attribute;
				""");
		});
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation || context.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol { IsStatic: true } method)
		{
			return null;
		}

		if (context.SemanticModel.GetOperation(invocation) is IInvocationOperation operation)
		{
			
		}

		foreach (var item in method.GetAttributes())
		{
			if (IsConstExprAttribute(item))
			{
				return GenerateExpression(context.SemanticModel.Compilation, invocation, method, token);
			}
		}

		return null;
	}

	private InvocationModel? GenerateExpression(Compilation compilation, InvocationExpressionSyntax invocation, IMethodSymbol methodDeclaration, CancellationToken token)
	{
		var methodSyntaxNode = GetMethodSyntaxNode(methodDeclaration);
		var variables = new Dictionary<string, object?>();

		for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			var parameter = invocation.ArgumentList.Arguments[i];
			var parameterName = methodDeclaration.Parameters[i].Name;

			if (!TryGetConstantValue(compilation, parameter.Expression, token, out var value))
			{
				return null;
			}

			variables.Add(parameterName, GetConstantValue(compilation, parameter.Expression, token));
		}

		var location = GetSemanticModel(compilation, invocation).GetInterceptableLocation(invocation, token);

		var constExprRewriter = new ConstExprRewriter(compilation, variables, token);
		var result = constExprRewriter
			.VisitMethodDeclaration(methodSyntaxNode)
			.NormalizeWhitespace("\t");

		return new InvocationModel
		{
			Usings = GetUsings(methodDeclaration),
			Method = methodSyntaxNode,
			Location = location,
			Node = result as MethodDeclarationSyntax,
		};
	}

	public MethodDeclarationSyntax? GetMethodSyntaxNode(IMethodSymbol methodSymbol)
	{
		var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
		var syntaxNode = syntaxReference?.GetSyntax() as MethodDeclarationSyntax;

		return syntaxNode;
	}

	public static HashSet<string> GetUsings(IMethodSymbol methodSymbol)
	{
		var usings = new HashSet<string>
		{
			"using System.Diagnostics;",
			"using System;",
			"using System.Runtime.CompilerServices;",
		};

		// Add the containing namespace
		if (methodSymbol.ContainingNamespace != null)
		{
			usings.Add($"using {methodSymbol.ContainingNamespace};");
		}
		
		var syntaxTree = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;

		if (syntaxTree != null)
		{
			var root = syntaxTree.GetRoot();
			var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();

			foreach (var usingDirective in usingDirectives)
			{
				usings.Add(usingDirective.ToString());
			}
		}

		return usings;
	}
}

#pragma warning restore RSEXPERIMENTAL002