using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToArray context.Method.
/// Optimizes patterns such as:
/// - collection.ToArray().ToArray() => collection.ToArray() (redundant ToArray)
/// - collection.ToList().ToArray() => collection.ToArray()
/// - collection.AsEnumerable().ToArray() => collection.ToArray()
/// </summary>
public class ToArrayFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToArray), 0)
{
	private static readonly HashSet<string> MaterializingMethods =
	[
		nameof(Enumerable.ToArray),
		nameof(Enumerable.ToList),
		nameof(Enumerable.AsEnumerable),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Skip all materializing/type-cast operations
		if (TryGetOptimizedChainExpression(source, MaterializingMethods, out source))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.ToArray));
			return true;
		}

		result = null;
		return false;
	}
}
