using System.Linq;
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

		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Min));
			return true;
		}
		
		result = null;
		return false;
	}
}

