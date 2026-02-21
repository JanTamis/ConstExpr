using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Prepend context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().Prepend(x) => new[] { x } or simplified form
/// - collection.Append(a).Prepend(b) => can be optimized for specific cases
/// </summary>
public class PrependFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Prepend), 1)
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

		if (IsEmptyEnumerable(context.Visit(source) ?? source))
		{
			result = CreateImplicitArray(context.VisitedParameters[0]);
		}

		result = null;
		return false;
	}
}

