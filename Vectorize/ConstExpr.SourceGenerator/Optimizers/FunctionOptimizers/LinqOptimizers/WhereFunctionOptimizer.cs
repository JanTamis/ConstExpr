using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Where context.Method.
/// Optimizes patterns such as:
/// - collection.Where(v => true) => collection (identity removal)
/// - collection.Where(v => false) => Enumerable.Empty&lt;T&gt;() (empty result)
/// - collection.Where(p1).Where(p2) => collection.Where(p1 && p2) (two chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3) => collection.Where(p1 && p2 && p3) (multiple chained Where statements)
/// </summary>
public class WhereFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Where), 1)
{
	private static readonly HashSet<string> OperationsThatDontAffectWhere =
	[
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: preserves all elements
		nameof(Enumerable.ToArray), // Materialization: preserves all elements
	];
	
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax> { lambda };
		var currentSource = source;

		if (TryExecutePredicates(context, currentSource, out result))
		{
			return true;
		}

		var isNewSource = TryGetOptimizedChainExpression(currentSource, OperationsThatDontAffectWhere, out currentSource);

		// Walk through the chain and collect all Where statements
		while (IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out var whereInvocation)
		       && TryGetLambda(whereInvocation.ArgumentList.Arguments[0].Expression, out var predicate)
		       && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			wherePredicates.Add(predicate);
			currentSource = whereSource;

			TryGetOptimizedChainExpression(source, OperationsThatDontAffectWhere, out source);
			isNewSource = true;
		}

		// If we found multiple Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// Start with the last predicate and combine with the rest
			var combinedPredicate = context.Visit(wherePredicates[^1]) as LambdaExpressionSyntax ?? wherePredicates[^1];
			
			// Combine from right to left (last to first)
			for (var i = wherePredicates.Count - 2; i >= 0; i--)
			{
				var currentPredicate = context.Visit(wherePredicates[i]) as LambdaExpressionSyntax ?? wherePredicates[i];
				combinedPredicate = CombinePredicates(currentPredicate, combinedPredicate);
			}
			
			combinedPredicate = context.Visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true:
						result = context.Visit(currentSource) ?? currentSource;
						return true;
					case false:
						result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
						return true;
				}
			}
			
			// Create a new Where call with the combined lambda
			result = UpdateInvocation(context, currentSource, combinedPredicate);
			return true;
		}

		if (IsLiteralBooleanLambda(lambda, out var parameterValue))
		{
			switch (parameterValue)
			{
				case true:
					result = context.Visit(source) ?? source;
					return true;
				case false:
					result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
					return true;
			}
		}
		
		if (isNewSource)
		{
			result = UpdateInvocation(context, source, lambda);
			return true;
		}

		result = null;
		return false;
	}
}
