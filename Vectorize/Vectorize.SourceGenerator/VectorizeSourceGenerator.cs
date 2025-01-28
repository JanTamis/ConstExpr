using System;
using System.Collections.Generic;
using System.Composition;
using System.Dynamic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using SGF;
using Vectorize.Rewriter;
using Vectorize.Rewriters;
using System.Linq;

namespace Vectorize;

[IncrementalGenerator]
public class VectorizeSourceGenerator() : IncrementalGenerator("Vectorize")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(x =>
		{
			//var method = context.SyntaxProvider.ForAttributeWithMetadataName("VectorizeAttribute", (node, token) => !token.IsCancellationRequested, GenerateSource);

			var method = context.SyntaxProvider.CreateSyntaxProvider((node, token) => node is InvocationExpressionSyntax, GenerateSource);

			context.RegisterSourceOutput(method, static (spc, source) =>
			{
				
			});
			
			x.AddSource("VectorizeAttribute.g", """
					using System;
					
					[AttributeUsage(AttributeTargets.Method, Inherited = false)]
					public class VectorizeAttribute : Attribute;
					""");

			x.AddSource("OptimizeAttribute.g", """
					using System;
					
					[AttributeUsage(AttributeTargets.Method, Inherited = false)]
					public class ConstExprAttribute : Attribute;
					""");
		});
	}

	private string? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation || context.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method)
		{
			return null;
		}

		foreach (var item in method.GetAttributes())
		{
			var name = item.AttributeClass.Name;
			
			if (name == "ConstExprAttribute")
			{
				GenerateExpression(context.SemanticModel, invocation, method, token);
			}
		}

		

		return String.Empty;
	}

	private string? GenerateExpression(SemanticModel semanticModel, InvocationExpressionSyntax invocation, IMethodSymbol methodDeclaration, CancellationToken token)
	{
		var methodSyntaxNode = GetMethodSyntaxNode(methodDeclaration);
		var variables = new Dictionary<string, object?>();

		for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			var parameter = invocation.ArgumentList.Arguments[i];
			var parameterName = methodDeclaration.Parameters[i].Name;
			
			variables.Add(parameterName, GetConstantValue(semanticModel, parameter.Expression, token));
		}
		
		var constExprRewriter = new ConstExprRewriter(semanticModel, methodSyntaxNode, variables, token);
		var result = constExprRewriter.VisitBlock(methodSyntaxNode.Body);

		return String.Empty;
	}

	public MethodDeclarationSyntax? GetMethodSyntaxNode(IMethodSymbol methodSymbol)
	{
		var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
		var syntaxNode = syntaxReference?.GetSyntax() as MethodDeclarationSyntax;
		
		return syntaxNode;
	}
	
	private object? GetConstantValue(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken token)
	{
		return expression switch 
		{
			LiteralExpressionSyntax literal => literal.Token.Value, 
			CollectionExpressionSyntax collection => collection.Elements
				.OfType<ExpressionElementSyntax>()
				.Select(x => GetConstantValue(semanticModel, x.Expression, token))
				.ToArray(),
			_ => null,
		};
	}
}