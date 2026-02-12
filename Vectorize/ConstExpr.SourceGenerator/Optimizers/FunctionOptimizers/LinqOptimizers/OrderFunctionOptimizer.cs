using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Order context.Method.
/// Optimizes patterns such as:
/// - collection.Order().Order() => collection.Order() (redundant order)
/// </summary>
public class OrderFunctionOptimizer() : BaseLinqFunctionOptimizer("Order", 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize Order().Order() => Order()
		if (IsLinqMethodChain(source, "Order", out var innerInvocation)
		    && TryGetLinqSource(innerInvocation, out _))
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		// Optimize OrderDescending().Order() => Order() (last one wins)
		if (IsLinqMethodChain(source, "OrderDescending", out var descInvocation)
		    && TryGetLinqSource(descInvocation, out var descSource))
		{
			result = CreateSimpleInvocation(context.Visit(descSource) ?? descSource, "Order");
			return true;
		}

		result = null;
		return false;
	}
}


