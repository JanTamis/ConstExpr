using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ExceptBy method.
/// Optimizes patterns such as:
/// - collection.ExceptBy(Enumerable.Empty&lt;T&gt;(), selector) => collection.DistinctBy(selector)
/// - Enumerable.Empty&lt;T&gt;().ExceptBy(collection, selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class ExceptByFunctionOptimizer() : BaseLinqFunctionOptimizer("DistinctBy", 2)
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
		var keySelector = parameters[1];

		// Optimize Enumerable.Empty<T>().ExceptBy(collection, selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		// Optimize collection.ExceptBy(Enumerable.Empty<TKey>(), selector) => collection.DistinctBy(selector)
		// (removing nothing means just keeping unique keys)
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateInvocation(visit(source) ?? source, "DistinctBy", keySelector);
			return true;
		}

		result = null;
		return false;
	}
}

