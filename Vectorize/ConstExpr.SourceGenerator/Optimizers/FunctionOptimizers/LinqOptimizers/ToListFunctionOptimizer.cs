using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToList context.Method.
/// Optimizes patterns such as:
/// - collection.ToList().ToList() => collection.ToList() (redundant ToList)
/// - collection.ToArray().ToList() => collection.ToList()
/// - collection.AsEnumerable().ToList() => collection.ToList()
/// </summary>
public class ToListFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToList), 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Skip all materializing/type-cast operations
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}
