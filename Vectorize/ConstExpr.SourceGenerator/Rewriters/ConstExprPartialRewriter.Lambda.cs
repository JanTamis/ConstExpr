using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
	private List<string> AddLambdaParameters(ExpressionSyntax node)
	{
		var addedParameters = new List<string>();

		if (!semanticModel.TryGetSymbol(node, symbolStore, out IMethodSymbol? method))
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

	/// <summary>
	/// Tries to evaluate a lambda stored in a local variable (marked CanBeInlined) when called
	/// with fully-constant arguments.  Returns the folded result literal, or null if evaluation
	/// is not possible.
	/// </summary>
	private SyntaxNode? TryEvaluateLambdaVariableWithArguments(
		LambdaExpressionSyntax lambda,
		IList<SyntaxNode> arguments,
		IMethodSymbol delegateMethod)
	{
		try
		{
			// Extract parameter names from the lambda syntax.
			var paramNames = lambda switch
			{
				SimpleLambdaExpressionSyntax simple =>
					[simple.Parameter.Identifier.Text],
				ParenthesizedLambdaExpressionSyntax parenthesized =>
					parenthesized.ParameterList.Parameters
						.Select(p => p.Identifier.Text)
						.ToArray(),
				_ => []
			};

			if (paramNames.Length != arguments.Count)
			{
				return null;
			}

			// Build a variable dictionary that inherits outer variables and adds the lambda params.
			var subParams = new Dictionary<string, VariableItem>(variables, StringComparer.Ordinal);

			for (var i = 0; i < paramNames.Length; i++)
			{
				var paramType = i < delegateMethod.Parameters.Length
					? delegateMethod.Parameters[i].Type
					: semanticModel.Compilation.ObjectType;

				subParams[paramNames[i]] = new VariableItem(paramType, false, arguments[i], true) { CanBeInlined = true };
			}

			// Create a child rewriter that evaluates the lambda body with the bound parameters.
			var subRewriter = new ConstExprPartialRewriter(
				semanticModel, loader, (_, _) => { }, subParams,
				additionalMethods, usings, attribute, symbolStore, token, visitingMethods);

			// Evaluate the body.
			if (lambda.Block is not null)
			{
				// Block-bodied lambda: prune dead code and look for a single return.
				var visitedBlock = subRewriter.Visit(lambda.Block) as BlockSyntax ?? lambda.Block;
				var pruned = DeadCodePruner.Prune(visitedBlock, subParams, semanticModel) as BlockSyntax;

				if (pruned?.Statements is [ReturnStatementSyntax { Expression: { } returnExpr }])
				{
					if (TryGetLiteralValue(returnExpr, out var retVal) && TryCreateLiteral(retVal, out var retLiteral))
					{
						return retLiteral;
					}
				}
			}
			else
			{
				// Expression-bodied lambda: visit and try to fold to a literal.
				if (subRewriter.Visit(lambda.Body) is ExpressionSyntax visitedExpr)
				{
					return visitedExpr;
				}
			}

			return null;
		}
		catch (Exception)
		{
			return null;
		}
	}
	
	private SyntaxNode? TryEvaluateLambdaVariableWithArguments(
		LambdaExpressionSyntax lambda,
		List<object> constantArguments,
		IMethodSymbol delegateMethod)
	{
		try
		{
			// Extract parameter names from the lambda syntax.
			var paramNames = lambda switch
			{
				SimpleLambdaExpressionSyntax simple =>
					[simple.Parameter.Identifier.Text],
				ParenthesizedLambdaExpressionSyntax parenthesized =>
					parenthesized.ParameterList.Parameters
						.Select(p => p.Identifier.Text)
						.ToArray(),
				_ => []
			};

			if (paramNames.Length != constantArguments.Count)
			{
				return null;
			}

			// Build a variable dictionary that inherits outer variables and adds the lambda params.
			var subParams = new Dictionary<string, VariableItem>(variables, StringComparer.Ordinal);

			for (var i = 0; i < paramNames.Length; i++)
			{
				var paramType = i < delegateMethod.Parameters.Length
					? delegateMethod.Parameters[i].Type
					: semanticModel.Compilation.ObjectType;

				subParams[paramNames[i]] = new VariableItem(paramType, hasValue: true, value: constantArguments[i])
				{
					IsInitialized = true,
				};
			}

			// Create a child rewriter that evaluates the lambda body with the bound parameters.
			var subRewriter = new ConstExprPartialRewriter(
				semanticModel, loader, (_, _) => { }, subParams,
				additionalMethods, usings, attribute, symbolStore, token, visitingMethods);

			// Evaluate the body.
			if (lambda.Block is not null)
			{
				// Block-bodied lambda: prune dead code and look for a single return.
				var visitedBlock = subRewriter.Visit(lambda.Block) as BlockSyntax ?? lambda.Block;
				var pruned = DeadCodePruner.Prune(visitedBlock, subParams, semanticModel) as BlockSyntax;

				if (pruned?.Statements is [ReturnStatementSyntax { Expression: { } returnExpr }])
				{
					if (TryGetLiteralValue(returnExpr, out var retVal) && TryCreateLiteral(retVal, out var retLiteral))
					{
						return retLiteral;
					}
				}
			}
			else
			{
				// Expression-bodied lambda: visit and try to fold to a literal.
				if (subRewriter.Visit(lambda.Body) is ExpressionSyntax visitedExpr && TryGetLiteralValue(visitedExpr, out var val) && TryCreateLiteral(val, out var literal))
				{
					return literal;
				}
			}

			return null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}

