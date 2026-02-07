using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SelectMany method.
/// Optimizes patterns such as:
/// - collection.SelectMany(x => Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;T&gt;()
/// - collection.SelectMany(x => new[] { x }) => collection (identity flattening)
/// - Enumerable.Empty&lt;T&gt;().SelectMany(selector) => Enumerable.Empty&lt;TResult&gt;()
/// </summary>
public class SelectManyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SelectMany), 1, 2)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Enumerable.Empty<T>().SelectMany(selector) => Enumerable.Empty<TResult>()
		if (IsEmptyEnumerable(source) && method.TypeArguments.Length > 0)
		{
			// Get the result type (last type argument)
			var resultType = method.TypeArguments[^1];
			result = CreateEmptyEnumerableCall(resultType);
			return true;
		}

		// Check if lambda always returns empty
		if (parameters.Count >= 1 && TryGetLambda(parameters[0], out var lambda))
		{
			if (TryGetLambdaBody(lambda, out var body) && IsEmptyEnumerable(body) && method.TypeArguments.Length > 0)
			{
				// selector always returns empty, so result is empty
				var resultType = method.TypeArguments[^1];
				result = CreateEmptyEnumerableCall(resultType);
				return true;
			}
		}

		result = null;
		return false;
	}
}

