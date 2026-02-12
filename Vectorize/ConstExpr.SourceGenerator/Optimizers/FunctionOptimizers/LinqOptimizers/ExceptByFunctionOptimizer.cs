using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ExceptBy context.Method.
/// Optimizes patterns such as:
/// - collection.ExceptBy(Enumerable.Empty&lt;T&gt;(), selector) => collection.DistinctBy(selector)
/// - Enumerable.Empty&lt;T&gt;().ExceptBy(collection, selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class ExceptByFunctionOptimizer() : BaseLinqFunctionOptimizer("DistinctBy", 2)
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
		var keySelector = context.VisitedParameters[1];

		// Optimize Enumerable.Empty<T>().ExceptBy(collection, selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		// Optimize collection.ExceptBy(Enumerable.Empty<TKey>(), selector) => collection.DistinctBy(selector)
		// (removing nothing means just keeping unique keys)
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateInvocation(context.Visit(source) ?? source, "DistinctBy", keySelector);
			return true;
		}

		result = null;
		return false;
	}
}

