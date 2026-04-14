using System.Diagnostics.CodeAnalysis;
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
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		var secondSource = context.VisitedParameters[0];

		// Optimize Enumerable.Empty<T>().IntersectBy(collection, selector) => Enumerable.Empty<T>()
		// Optimize collection.IntersectBy(Enumerable.Empty<TKey>(), selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source)
		    || IsEmptyEnumerable(secondSource))
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

