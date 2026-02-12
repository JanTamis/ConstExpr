using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.MinBy context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().MinBy(selector) - cannot optimize (throws exception)
/// </summary>
public class MinByFunctionOptimizer() : BaseLinqFunctionOptimizer("MinBy", 1)
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

		// MinBy with identity lambda can be optimized to just getting the Min
		// However, MinBy returns the element, not the key, so we need to be careful
		// For now, no safe optimization
		result = null;
		return false;
	}
}

