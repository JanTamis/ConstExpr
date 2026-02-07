using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.DistinctBy method.
/// Optimizes patterns such as:
/// - collection.DistinctBy(x => x) => collection.Distinct() (identity key selector)
/// - Enumerable.Empty&lt;T&gt;().DistinctBy(selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class DistinctByFunctionOptimizer() : BaseLinqFunctionOptimizer("DistinctBy", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize DistinctBy(x => x) => Distinct()
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(visit(source) ?? source, nameof(Enumerable.Distinct));
			return true;
		}

		// Optimize Enumerable.Empty<T>().DistinctBy(selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = visit(source);
			return true;
		}

		result = null;
		return false;
	}
}
