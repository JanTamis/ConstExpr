using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OrderDescending context.Method.
/// Optimizes patterns such as:
/// - collection.OrderDescending().OrderDescending() => collection.OrderDescending() (redundant order)
/// </summary>
public class OrderDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer("OrderDescending", 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize OrderDescending().OrderDescending() => OrderDescending()
		if (IsLinqMethodChain(source, "OrderDescending", out var innerInvocation)
		    && TryGetLinqSource(innerInvocation, out _))
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		// Optimize Order().OrderDescending() => OrderDescending() (last one wins)
		if (IsLinqMethodChain(source, "Order", out var orderInvocation)
		    && TryGetLinqSource(orderInvocation, out var orderSource))
		{
			result = CreateSimpleInvocation(context.Visit(orderSource) ?? orderSource, "OrderDescending");
			return true;
		}

		result = null;
		return false;
	}
}


