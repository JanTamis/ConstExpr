using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipLast context.Method.
/// Optimizes patterns such as:
/// - collection.SkipLast(0) => collection (skip nothing)
/// - collection.SkipLast(n).SkipLast(m) => collection.SkipLast(n + m)
/// </summary>
public class SkipLastFunctionOptimizer() : BaseLinqFunctionOptimizer("SkipLast", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || context.Invocation.Expression is not MemberAccessExpressionSyntax memberAccess
		    || context.VisitedParameters[0] is not LiteralExpressionSyntax { Token.Value: int count })
		{
			result = null;
			return false;
		}

		// Optimize SkipLast(0) => source (skip nothing)
		if (count <= 0)
		{
			result = context.Visit(memberAccess.Expression) ?? memberAccess.Expression;
			return true;
		}

		result = null;
		return false;
	}
}

