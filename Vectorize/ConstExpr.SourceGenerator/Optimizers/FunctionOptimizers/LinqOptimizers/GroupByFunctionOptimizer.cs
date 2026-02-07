using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.GroupBy method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().GroupBy(selector) => Enumerable.Empty&lt;IGrouping&lt;TKey, T&gt;&gt;()
/// </summary>
public class GroupByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.GroupBy), 1, 2, 3)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Enumerable.Empty<T>().GroupBy(selector) => Enumerable.Empty<IGrouping<TKey, T>>()
		if (IsEmptyEnumerable(source) && method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

