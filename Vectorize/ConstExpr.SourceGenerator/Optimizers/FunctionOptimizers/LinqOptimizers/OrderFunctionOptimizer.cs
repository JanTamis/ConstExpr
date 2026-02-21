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

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case "Order":
				{
					result = context.Visit(invocationSource) ?? invocationSource;
					return true;
				}
				case "OrderDescending":
				{
					result = CreateSimpleInvocation(context.Visit(invocationSource) ?? invocationSource, "Order");
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}