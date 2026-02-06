using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Reverse method.
/// Optimizes patterns such as:
/// - collection.Reverse().Reverse() => collection (double reverse cancels out)
/// </summary>
public class ReverseFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Reverse), 0)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Reverse().Reverse() => original collection (double reverse cancels out)
		if (IsLinqMethodChain(source, nameof(Enumerable.Reverse), out var reverseInvocation)
		    && TryGetLinqSource(reverseInvocation, out var reverseSource))
		{
			result = reverseSource;
			return true;
		}
		
		result = null;
		return false;
	}
}
