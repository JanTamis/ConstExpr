using System.Collections.Generic;
using System.Linq;
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
	private static readonly HashSet<string> OrderingOperations =
	[
		..MaterializingMethods,
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending"
	];
	
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
		
		var isNewSource = TryGetOptimizedChainExpression(source, OrderingOperations, out source);

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case "OrderDescending":
				{
					result = invocationSource;
					return true;
				}
				case "Order":
				{
					result = UpdateInvocation(context, invocationSource);
					return true;
				}
			}
		}
		
		if (isNewSource)
		{
			result = CreateSimpleInvocation(source, "OrderDescending");
			return true;
		}

		result = null;
		return false;
	}
}


