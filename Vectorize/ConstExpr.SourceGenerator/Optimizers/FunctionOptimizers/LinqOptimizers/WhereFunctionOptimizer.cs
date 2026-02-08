using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Where method.
/// Optimizes patterns such as:
/// - collection.Where(v => true) => collection (identity removal)
/// - collection.Where(v => false) => Enumerable.Empty&lt;T&gt;() (empty result)
/// - collection.Where(p1).Where(p2) => collection.Where(p1 && p2) (two chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3) => collection.Where(p1 && p2 && p3) (multiple chained Where statements)
/// </summary>
public class WhereFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Where), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax> { lambda };
		var currentSource = source;

		// Walk through the chain and collect all Where statements
		while (IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out var whereInvocation)
		       && TryGetLambda(whereInvocation.ArgumentList.Arguments[0].Expression, out var predicate)
		       && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			wherePredicates.Add(predicate);
			currentSource = whereSource;
		}

		// If we found multiple Where predicates, combine them
		if (wherePredicates.Count > 1)
		{
			// Start with the last predicate and combine with the rest
			var combinedPredicate = visit(wherePredicates[^1]) as LambdaExpressionSyntax ?? wherePredicates[^1];
			
			// Combine from right to left (last to first)
			for (var i = wherePredicates.Count - 2; i >= 0; i--)
			{
				var currentPredicate = visit(wherePredicates[i]) as LambdaExpressionSyntax ?? wherePredicates[i];
				combinedPredicate = CombinePredicates(currentPredicate, combinedPredicate);
			}
			
			combinedPredicate = visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true:
						result = visit(currentSource) ?? currentSource;
						return true;
					case false:
						result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
						return true;
				}

			}

			// Create a new Where call with the combined lambda
			result = CreateInvocation(visit(currentSource) ?? currentSource, nameof(Enumerable.Where), combinedPredicate);
			return true;
		}

		if (IsLiteralBooleanLambda(lambda, out var parameterValue))
		{
			switch (parameterValue)
			{
				case true:
					result = visit(source) ?? source;
					return true;
				case false:
					result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
					return true;
			}
		}

		result = null;
		return false;
	}

	private static bool IsAlwaysTrueLambda(LambdaExpressionSyntax lambda)
	{
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		return body is LiteralExpressionSyntax { Token.Value: true };
	}

	private static bool IsAlwaysFalseLambda(LambdaExpressionSyntax lambda)
	{
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax expr } => expr,
			_ => null
		};

		return body is LiteralExpressionSyntax { Token.Value: false };
	}
}
