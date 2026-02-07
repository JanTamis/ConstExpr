using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.MaxBy method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().MaxBy(selector) - cannot optimize (throws exception)
/// </summary>
public class MaxByFunctionOptimizer() : BaseLinqFunctionOptimizer("MaxBy", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// MaxBy with identity lambda can be optimized to just getting the Max
		// However, MaxBy returns the element, not the key, so we need to be careful
		// For now, no safe optimization
		result = null;
		return false;
	}
}

