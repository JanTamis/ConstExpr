using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Order method.
/// Optimizes patterns such as:
/// - collection.Order().Order() => collection.Order() (redundant order)
/// </summary>
public class OrderFunctionOptimizer() : BaseLinqFunctionOptimizer("Order", 0)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Order().Order() => Order()
		if (IsLinqMethodChain(source, "Order", out var innerInvocation)
		    && TryGetLinqSource(innerInvocation, out _))
		{
			result = source;
			return true;
		}

		// Optimize OrderDescending().Order() => Order() (last one wins)
		if (IsLinqMethodChain(source, "OrderDescending", out var descInvocation)
		    && TryGetLinqSource(descInvocation, out var descSource))
		{
			result = CreateSimpleInvocation(descSource, "Order");
			return true;
		}

		result = null;
		return false;
	}
}


