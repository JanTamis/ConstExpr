using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SingleOrDefault context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).SingleOrDefault() => collection.SingleOrDefault(predicate)
/// - collection.AsEnumerable().SingleOrDefault() => collection.SingleOrDefault()
/// - collection.ToList().SingleOrDefault() => collection.SingleOrDefault()
/// </summary>
public class SingleOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SingleOrDefault), 0, 1)
{
	// Operations that don't affect which element is "single"
	private static readonly HashSet<string> OperationsThatDontAffectSingleOrDefault =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect singleOrDefault
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSingleOrDefault, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize source.Where(predicate).SingleOrDefault() => source.SingleOrDefault(predicate)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && TryGetLinqSource(whereInvocation, out var whereSource)
		    && whereInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectSingleOrDefault, out whereSource);
			
			var predicate = whereInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(context.Visit(whereSource) ?? whereSource, nameof(Enumerable.SingleOrDefault), context.Visit(predicate) ?? predicate);
			return true;
		}

		// If we skipped any operations, create optimized SingleOrDefault() call
		if (isNewSource && context.VisitedParameters.Count == 0)
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.SingleOrDefault));
			return true;
		}

		result = null;
		return false;
	}
}
