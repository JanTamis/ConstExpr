using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderBy), 1)
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

		// Optimize OrderBy(x => x) => Order() (identity lambda)
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, "Order");
			return true;
		}
		
		result = null;
		return false;
	}
}