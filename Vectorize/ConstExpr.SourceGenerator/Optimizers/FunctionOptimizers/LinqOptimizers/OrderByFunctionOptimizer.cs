using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderBy), 1)
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
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
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

		// Optimize OrderBy(x => x) => Order() (identity lambda)
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(source, "Order");
			return true;
		}
		
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.OrderBy), lambda);
			return true;
		}
		
		result = null;
		return false;
	}
}