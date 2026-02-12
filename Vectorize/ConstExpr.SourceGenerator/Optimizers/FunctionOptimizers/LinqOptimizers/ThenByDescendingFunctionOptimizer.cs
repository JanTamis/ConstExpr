using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ThenByDescending context.Method.
/// Optimizes patterns such as:
/// - OrderBy(x => x).ThenByDescending(y => y) => Order().ThenByDescending(y => y) (identity key for Order)
/// </summary>
public class ThenByDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ThenByDescending), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize ThenByDescending(x => x) identity lambda - not much to optimize here
		// ThenByDescending is usually semantically significant
		result = null;
		return false;
	}
}

