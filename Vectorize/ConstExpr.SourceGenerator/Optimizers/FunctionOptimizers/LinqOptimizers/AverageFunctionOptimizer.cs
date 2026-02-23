using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Average context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Average() =&gt; collection.Average() (skip type cast)
/// - collection.ToList().Average() =&gt; collection.Average() (skip materialization)
/// - collection.ToArray().Average() =&gt; collection.Average() (skip materialization)
/// - collection.OrderBy(...).Average() =&gt; collection.Average() (ordering doesn't affect average)
/// - collection.Reverse().Average() =&gt; collection.Average() (reversing doesn't affect average)
/// </summary>
public class AverageFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Average), 0, 1)
{
	// Operations that don't affect Average behavior
	private static readonly HashSet<string> OperationsThatDontAffectAverage =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// If we skipped any operations, create optimized Average call
		if (TryGetOptimizedChainExpression(source, OperationsThatDontAffectAverage, out source))
		{
			if (TryExecutePredicates(context, source, out result))
			{
				return true;
			}

			source = context.Visit(source) ?? source;
		
			if (IsEmptyEnumerable(source))
			{
				// Average of an empty sequence throws an exception, so we return a throw expression instead of optimizing to Average() which would be incorrect
				result = CreateThrowExpression<InvalidOperationException>("Sequence contains no elements");
				return true;
			}

			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}
