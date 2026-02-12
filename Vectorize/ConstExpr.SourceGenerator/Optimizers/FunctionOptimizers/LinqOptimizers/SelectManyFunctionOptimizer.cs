using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SelectMany context.Method.
/// Optimizes patterns such as:
/// - collection.SelectMany(x => Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;T&gt;()
/// - collection.SelectMany(x => new[] { x }) => collection (identity flattening)
/// - Enumerable.Empty&lt;T&gt;().SelectMany(selector) => Enumerable.Empty&lt;TResult&gt;()
/// </summary>
public class SelectManyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SelectMany), 1, 2)
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

		// Optimize Enumerable.Empty<T>().SelectMany(selector) => Enumerable.Empty<TResult>()
		if (IsEmptyEnumerable(source) && context.Method.TypeArguments.Length > 0)
		{
			// Get the result type (last type argument)
			var resultType = context.Method.TypeArguments[^1];
			result = CreateEmptyEnumerableCall(resultType);
			return true;
		}

		// Check if lambda always returns empty
		if (context.VisitedParameters.Count >= 1 && TryGetLambda(context.VisitedParameters[0], out var lambda))
		{
			if (TryGetLambdaBody(lambda, out var body) && IsEmptyEnumerable(body) && context.Method.TypeArguments.Length > 0)
			{
				// selector always returns empty, so result is empty
				var resultType = context.Method.TypeArguments[^1];
				
				result = CreateEmptyEnumerableCall(resultType);
				return true;
			}
		}

		result = null;
		return false;
	}
}

