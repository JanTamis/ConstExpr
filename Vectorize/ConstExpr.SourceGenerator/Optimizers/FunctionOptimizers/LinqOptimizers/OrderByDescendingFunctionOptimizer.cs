using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderByDescending), 1)
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

		if (IsIdentityLambda(lambda))
		{
			result = TryOptimizeByOptimizer<OrderDescendingFunctionOptimizer>(context, CreateSimpleInvocation(source, "OrderDescending"));
			return true;
		}
		
		if (isNewSource)
		{
			result = CreateInvocation(source, Name, lambda);
			return true;
		}

		result = null;
		return false;
	}
}