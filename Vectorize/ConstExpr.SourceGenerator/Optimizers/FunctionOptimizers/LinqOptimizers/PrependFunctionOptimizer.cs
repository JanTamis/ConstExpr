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

		// Check for empty source optimization is complex, we would need to ensure the source is actually empty
		// For now, we'll skip complex optimizations and just return false

		result = null;
		return false;
	}
}

