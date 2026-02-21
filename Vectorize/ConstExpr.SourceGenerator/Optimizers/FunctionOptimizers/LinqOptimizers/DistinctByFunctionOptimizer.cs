using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.DistinctBy context.Method.
/// Optimizes patterns such as:
/// - collection.DistinctBy(x => x) => collection.Distinct() (identity key selector)
/// - Enumerable.Empty&lt;T&gt;().DistinctBy(selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class DistinctByFunctionOptimizer() : BaseLinqFunctionOptimizer("DistinctBy", 1)
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

		// Optimize DistinctBy(x => x) => Distinct()
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Distinct));
			return true;
		}

		result = null;
		return false;
	}
}
