using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.UnionBy method.
/// Optimizes patterns such as:
/// - collection.UnionBy(Enumerable.Empty&lt;T&gt;(), selector) => collection.DistinctBy(selector)
/// - Enumerable.Empty&lt;T&gt;().UnionBy(collection, selector) => collection.DistinctBy(selector)
/// - collection.UnionBy(collection, selector) => collection.DistinctBy(selector) (same source)
/// </summary>
public class UnionByFunctionOptimizer() : BaseLinqFunctionOptimizer("UnionBy", 2)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = parameters[0];
		var keySelector = parameters[1];

		// Optimize collection.UnionBy(Enumerable.Empty<T>(), selector) => collection.DistinctBy(selector)
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateInvocation(source, "DistinctBy", keySelector);
			return true;
		}

		// Optimize Enumerable.Empty<T>().UnionBy(collection, selector) => collection.DistinctBy(selector)
		if (IsEmptyEnumerable(source))
		{
			result = CreateInvocation(secondSource, "DistinctBy", keySelector);
			return true;
		}

		// Optimize collection.UnionBy(collection, selector) => collection.DistinctBy(selector) (same reference)
		if (AreSyntacticallyEquivalent(source, secondSource))
		{
			result = CreateInvocation(source, "DistinctBy", keySelector);
			return true;
		}

		result = null;
		return false;
	}
}

