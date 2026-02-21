using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Reverse context.Method.
/// Optimizes patterns such as:
/// - collection.Reverse().Reverse() => collection (double reverse cancels out)
/// </summary>
public class ReverseFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Reverse), 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize Reverse().Reverse() => original collection (double reverse cancels out)
		if (IsLinqMethodChain(source, nameof(Enumerable.Reverse), out var reverseInvocation)
		    && TryGetLinqSource(reverseInvocation, out var reverseSource))
		{
			result = context.Visit(reverseSource) ?? reverseSource;
			return true;
		}
		
		result = null;
		return false;
	}
}
