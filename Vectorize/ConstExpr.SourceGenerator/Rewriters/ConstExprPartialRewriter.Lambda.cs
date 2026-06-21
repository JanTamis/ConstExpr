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
///   Lambda expression visitor methods for the ConstExprPartialRewriter.
///   Handles simple and parenthesized lambda expressions.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		var addedParameters = AddLambdaParameters(node);

		if (node.Block is not null)
		{
			var block = Visit(node.Block);
			node = node.WithBlock(block as BlockSyntax ?? node.Block);
		}
		else
		{
			var body = Visit(node.Body);
			node = node.WithBody(body as CSharpSyntaxNode ?? node.Body);
		}

		RemoveLambdaParameters(addedParameters);

		if (semanticModel.TryGetMethodSymbol(node, symbolStore, out var method))
		{
			return node
				.WithParameter(node.Parameter.WithTypeSymbolAnnotation(method.Parameters[0].Type, symbolStore))
				.WithMethodSymbolAnnotation(method, symbolStore);
		}

		return node;
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

		return semanticModel.TryGetMethodSymbol(node, symbolStore, out var method)
			? result.WithMethodSymbolAnnotation(method, symbolStore)
			: result;
	}

	/// <summary>
	///   Adds lambda parameters to the variables dictionary, shadowing any outer variables with the same name.
	///   Returns a list of (name, previous value or null) pairs for restoration.
	/// </summary>
	private List<(string Name, VariableItem? Previous)> AddLambdaParameters(ExpressionSyntax node)
	{
		var addedParameters = new List<(string, VariableItem?)>();

		if (semanticModel.TryGetSymbol(node, symbolStore, out IMethodSymbol? method))
		{
			// Symbol info available: always add all parameters with their correct types,
			// shadowing any outer variables with the same name.
			foreach (var parameter in method.Parameters)
			{
				variables.TryGetValue(parameter.Name, out var previous);
				variables[parameter.Name] = new VariableItem(parameter.Type, false, null, true);
				addedParameters.Add((parameter.Name, previous));
			}
		}

		// No symbol info (generated/synthetic lambda): revert to old behavior.
		// Do NOT add variables here — writing ObjectType to the shared symbolStore
		// would corrupt concrete-type annotations for same-named identifiers
		// that were previously annotated (e.g. breaking range check optimization
		// where 'v: int' → 'v: object' would prevent TryGetUnsignedType from working).
		return addedParameters;
	}

	/// <summary>
	///   Removes lambda parameters from the variables dictionary, restoring any shadowed outer variables.
	/// </summary>
	private void RemoveLambdaParameters(List<(string Name, VariableItem? Previous)> addedParameters)
	{
		foreach (var (name, previous) in addedParameters)
		{
			if (previous is not null)
			{
				variables[name] = previous;
			}
			else
			{
				variables.Remove(name);
			}
		}
	}

	/// <summary>
	///   Tries to evaluate a lambda stored in a local variable (marked CanBeInlined) when called
	///   with fully-constant arguments.  Returns the folded result literal, or null if evaluation
	///   is not possible.
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
					[ simple.Parameter.Identifier.Text ],
				ParenthesizedLambdaExpressionSyntax parenthesized =>
					parenthesized.ParameterList.Parameters
						.Select(p => p.Identifier.Text)
						.ToArray(),
				_ => [ ]
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

				if (pruned?.Statements is [ ReturnStatementSyntax { Expression: { } returnExpr } ])
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
					[ simple.Parameter.Identifier.Text ],
				ParenthesizedLambdaExpressionSyntax parenthesized =>
					parenthesized.ParameterList.Parameters
						.Select(p => p.Identifier.Text)
						.ToArray(),
				_ => [ ]
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

				subParams[paramNames[i]] = new VariableItem(paramType, true, constantArguments[i])
				{
					IsInitialized = true
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

				if (pruned?.Statements is [ ReturnStatementSyntax { Expression: { } returnExpr } ])
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