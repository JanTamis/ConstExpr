using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Cast context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Cast&lt;T&gt;() =&gt; collection.Cast&lt;T&gt;() (skip type cast)
/// - collection.ToList().Cast&lt;T&gt;() =&gt; collection.Cast&lt;T&gt;() (skip materialization)
/// - collection.ToArray().Cast&lt;T&gt;() =&gt; collection.Cast&lt;T&gt;() (skip materialization)
/// </summary>
public class CastFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Cast), 0)
{
	// Operations that don't affect Cast behavior (type casts and materializations)
	private static readonly HashSet<string> OperationsThatDontAffectCast =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Cast call
		if (TryGetOptimizedChainExpression(source, MaterializingMethods, out source))
		{
			if (TryExecutePredicates(context, source, out result, out _))
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
