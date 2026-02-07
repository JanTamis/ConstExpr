using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.IntersectBy method.
/// Optimizes patterns such as:
/// - collection.IntersectBy(Enumerable.Empty&lt;TKey&gt;(), selector) => Enumerable.Empty&lt;T&gt;()
/// - Enumerable.Empty&lt;T&gt;().IntersectBy(collection, selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class IntersectByFunctionOptimizer() : BaseLinqFunctionOptimizer("IntersectBy", 2)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = parameters[0];

		// Optimize Enumerable.Empty<T>().IntersectBy(collection, selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		// Optimize collection.IntersectBy(Enumerable.Empty<TKey>(), selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

