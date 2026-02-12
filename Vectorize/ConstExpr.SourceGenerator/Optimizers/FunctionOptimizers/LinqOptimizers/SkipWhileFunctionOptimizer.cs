using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipWhile context.Method.
/// Optimizes patterns such as:
/// - collection.SkipWhile(x => false) => collection (skip nothing)
/// - collection.SkipWhile(x => true) => Enumerable.Empty&lt;T&gt;() (skip everything)
/// </summary>
public class SkipWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SkipWhile), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize SkipWhile(x => false) => collection (never skip anything)
		if (IsLiteralBooleanLambda(lambda, out var value) && value == false)
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		// Optimize SkipWhile(x => true) => Enumerable.Empty<T>() (skip everything)
		if (IsLiteralBooleanLambda(lambda, out value) && value == true)
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

