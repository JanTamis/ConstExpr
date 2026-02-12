using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.IntersectBy context.Method.
/// Optimizes patterns such as:
/// - collection.IntersectBy(Enumerable.Empty&lt;TKey&gt;(), selector) => Enumerable.Empty&lt;T&gt;()
/// - Enumerable.Empty&lt;T&gt;().IntersectBy(collection, selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class IntersectByFunctionOptimizer() : BaseLinqFunctionOptimizer("IntersectBy", 2)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = context.VisitedParameters[0];

		// Optimize Enumerable.Empty<T>().IntersectBy(collection, selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		// Optimize collection.IntersectBy(Enumerable.Empty<TKey>(), selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

