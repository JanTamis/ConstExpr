using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
	// Operations that don't affect Append behavior (type casts and materializations)
	private static readonly HashSet<string> OperationsThatDontAffectAppend =
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

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Append call
		if (TryGetOptimizedChainExpression(source, OperationsThatDontAffectAppend, out source))
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Append), context.VisitedParameters[0]);
			return true;
		}

		result = null;
		return false;
	}
}
