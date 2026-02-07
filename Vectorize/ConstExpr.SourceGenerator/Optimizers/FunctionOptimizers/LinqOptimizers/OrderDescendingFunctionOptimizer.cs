using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OrderDescending method.
/// Optimizes patterns such as:
/// - collection.OrderDescending().OrderDescending() => collection.OrderDescending() (redundant order)
/// </summary>
public class OrderDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer("OrderDescending", 0)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize OrderDescending().OrderDescending() => OrderDescending()
		if (IsLinqMethodChain(source, "OrderDescending", out var innerInvocation)
		    && TryGetLinqSource(innerInvocation, out _))
		{
			result = visit(source) ?? source;
			return true;
		}

		// Optimize Order().OrderDescending() => OrderDescending() (last one wins)
		if (IsLinqMethodChain(source, "Order", out var orderInvocation)
		    && TryGetLinqSource(orderInvocation, out var orderSource))
		{
			result = CreateSimpleInvocation(visit(orderSource) ?? orderSource, "OrderDescending");
			return true;
		}

		result = null;
		return false;
	}
}


