using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderByDescending), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
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

		if (IsIdentityLambda(lambda))
		{
			result = TryOptimizeByOptimizer<OrderDescendingFunctionOptimizer>(context, CreateSimpleInvocation(source, "OrderDescending"));
			return true;
		}

		result = null;
		return false;
	}
}