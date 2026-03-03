using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Order context.Method.
/// Optimizes patterns such as:
/// - collection.Order().Order() => collection.Order() (redundant order)
/// </summary>
public class OrderFunctionOptimizer() : BaseLinqFunctionOptimizer("Order", 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case "Order":
				{
					result = invocationSource;
					return true;
				}
				case "OrderDescending":
				{
					result = CreateSimpleInvocation(invocationSource, "Order");
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}