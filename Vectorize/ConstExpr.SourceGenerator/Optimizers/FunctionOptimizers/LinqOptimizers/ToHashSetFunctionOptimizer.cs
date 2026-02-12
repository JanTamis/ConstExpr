using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToHashSet context.Method.
/// Optimizes patterns such as:
/// - collection.ToHashSet().ToHashSet() => collection.ToHashSet() (redundant ToHashSet)
/// - collection.Distinct().ToHashSet() => collection.ToHashSet() (Distinct is implicit in HashSet)
/// - collection.AsEnumerable().ToHashSet() => collection.ToHashSet()
/// - collection.ToList().ToHashSet() => collection.ToHashSet()
/// </summary>
public class ToHashSetFunctionOptimizer() : BaseLinqFunctionOptimizer("ToHashSet", 0)
{
	private static readonly HashSet<string> OperationsThatDontAffectToHashSet =
	[
		"ToHashSet",
		nameof(Enumerable.Distinct),
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

		// Skip all operations that don't affect ToHashSet result
		if (TryGetOptimizedChainExpression(source, OperationsThatDontAffectToHashSet, out source))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, "ToHashSet");
			return true;
		}

		result = null;
		return false;
	}
}

