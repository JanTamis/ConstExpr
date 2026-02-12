using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Min context.Method.
/// Optimizes patterns such as:
/// - collection.Min(x => x) => collection.Min() (identity lambda removal)
/// - collection.Select(x => x.Property).Min() => collection.Min(x => x.Property)
/// - collection.OrderBy(...).Min() => collection.Min() (ordering doesn't affect min)
/// - collection.AsEnumerable().Min() => collection.Min()
/// - collection.ToList().Min() => collection.Min()
/// </summary>
public class MinFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Min), 0, 1)
{
	// Operations that don't affect the minimum value
	private static readonly HashSet<string> OperationsThatDontAffectMin =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect min
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMin, out source);

		// Optimize Min(x => x) => Min() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(context.Visit(lambda) as LambdaExpressionSyntax ?? lambda))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Min));
			return true;
		}

		// Optimize source.Select(selector).Min() => source.Min(selector)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && TryGetLinqSource(selectInvocation, out var selectSource)
		    && selectInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectMin, out selectSource);
			
			var selector = selectInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(context.Visit(selectSource) ?? selectSource, nameof(Enumerable.Min), context.Visit(selector) ?? selector);
			return true;
		}

		// If we skipped any operations, create optimized Min() call
		if (isNewSource && context.VisitedParameters.Count == 0)
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Min));
			return true;
		}

		result = null;
		return false;
	}
}