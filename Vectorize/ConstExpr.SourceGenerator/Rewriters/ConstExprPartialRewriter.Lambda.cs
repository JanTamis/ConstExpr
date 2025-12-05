using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Lambda expression visitor methods for the ConstExprPartialRewriter.
/// Handles simple and parenthesized lambda expressions.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		var addedParameters = AddLambdaParameters(node);

		SyntaxNode? result;

		if (node.Block is not null)
		{
			var block = Visit(node.Block);
			result = node.WithBlock(block as BlockSyntax ?? node.Block);
		}
		else
		{
			var body = Visit(node.Body);
			result = node.WithBody(body as CSharpSyntaxNode ?? node.Body);
		}

		RemoveLambdaParameters(addedParameters);

		return result;
	}

	public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
	{
		var addedParameters = AddLambdaParameters(node);

		SyntaxNode? result;

		if (node.Block is not null)
		{
			var block = Visit(node.Block);
			result = node.WithBlock(block as BlockSyntax ?? node.Block);
		}
		else
		{
			var body = Visit(node.Body);
			result = node.WithBody(body as CSharpSyntaxNode ?? node.Body);
		}

		RemoveLambdaParameters(addedParameters);

		return result;
	}

	/// <summary>
	/// Adds lambda parameters to the variables dictionary.
	/// </summary>
	private List<string> AddLambdaParameters(SyntaxNode node)
	{
		var addedParameters = new List<string>();

		if (!semanticModel.TryGetSymbol(node, out IMethodSymbol? method))
		{
			return addedParameters;
		}

		foreach (var methodParameter in method.Parameters)
		{
			if (!variables.ContainsKey(methodParameter.Name))
			{
				variables.Add(methodParameter.Name, new VariableItem(methodParameter.Type, false, null, true));
				addedParameters.Add(methodParameter.Name);
			}
		}

		return addedParameters;
	}

	/// <summary>
	/// Removes lambda parameters from the variables dictionary.
	/// </summary>
	private void RemoveLambdaParameters(List<string> addedParameters)
	{
		foreach (var param in addedParameters)
		{
			variables.Remove(param);
		}
	}
}

