using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Union method.
/// Optimizes patterns such as:
/// - collection.Union(Enumerable.Empty&lt;T&gt;()) => collection.Distinct() (union with empty)
/// - Enumerable.Empty&lt;T&gt;().Union(collection) => collection.Distinct()
/// - collection.Union(collection) => collection.Distinct() (same source)
/// </summary>
public class UnionFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Union), 1)
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

		// Optimize collection.Union(Enumerable.Empty<T>()) => collection.Distinct()
		if (IsEmptyEnumerable(secondSource))
		{
			result = CreateSimpleInvocation(source, nameof(Enumerable.Distinct));
			return true;
		}

		// Optimize Enumerable.Empty<T>().Union(collection) => collection.Distinct()
		if (IsEmptyEnumerable(source))
		{
			result = CreateSimpleInvocation(secondSource, nameof(Enumerable.Distinct));
			return true;
		}

		// Optimize collection.Union(collection) => collection.Distinct() (same reference)
		if (AreSyntacticallyEquivalent(source, secondSource))
		{
			result = CreateSimpleInvocation(source, nameof(Enumerable.Distinct));
			return true;
		}

		result = null;
		return false;
	}
}

