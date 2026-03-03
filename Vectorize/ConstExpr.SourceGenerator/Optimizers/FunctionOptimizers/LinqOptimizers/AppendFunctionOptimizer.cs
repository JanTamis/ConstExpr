using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Append context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Append(x) =&gt; collection.Append(x) (skip type cast)
/// - collection.ToList().Append(x) =&gt; collection.Append(x) (skip materialization)
/// - collection.ToArray().Append(x) =&gt; collection.Append(x) (skip materialization)
/// </summary>
public class AppendFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Append), 1)
{
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

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Append call
		if (TryGetOptimizedChainExpression(source, MaterializingMethods, out source))
		{
			if (TryExecutePredicates(context, source, out result, out source))
			{
				return true;
			}

			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}
