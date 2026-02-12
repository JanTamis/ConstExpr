using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Cast call
		if (TryGetOptimizedChainExpression(source, OperationsThatDontAffectCast, out source))
		{
			// Preserve the generic type argument from the original Cast<T>() call
			if (context.Invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
			{
				result = CreateInvocation(context.Visit(source) ?? source, genericName);
			}
			else
			{
				result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Cast));
			}
			
			return true;
		}

		result = null;
		return false;
	}
}
