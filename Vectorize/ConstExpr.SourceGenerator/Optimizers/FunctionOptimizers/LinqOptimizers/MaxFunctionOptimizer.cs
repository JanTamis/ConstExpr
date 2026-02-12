using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Max context.Method.
/// Optimizes patterns such as:
/// - collection.Max(x => x) => collection.Max() (identity lambda removal)
/// - collection.Select(x => x.Property).Max() => collection.Max(x => x.Property)
/// - collection.OrderBy(...).Max() => collection.Max() (ordering doesn't affect max)
/// - collection.AsEnumerable().Max() => collection.Max()
/// - collection.ToList().Max() => collection.Max()
/// </summary>
public class MaxFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Max), 0, 1)
{
	// Operations that don't affect the maximum value
	private static readonly HashSet<string> OperationsThatDontAffectMax =
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

		// Recursively skip operations that don't affect max
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMax, out source);

		// Optimize Max(x => x) => Max() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(context.Visit(lambda) as LambdaExpressionSyntax ?? lambda ))
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Max));
			return true;
		}

		// Optimize source.Select(selector).Max() => source.Max(selector)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && TryGetLinqSource(selectInvocation, out var selectSource)
		    && selectInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectMax, out selectSource);
			
			var selector = selectInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(context.Visit(selectSource) ?? selectSource, nameof(Enumerable.Max), context.Visit(selector) ?? selector);
			return true;
		}

		// If we skipped any operations, create optimized Max() call
		if (isNewSource && context.VisitedParameters.Count == 0)
		{
			result = CreateSimpleInvocation(context.Visit(source) ?? source, nameof(Enumerable.Max));
			return true;
		}

		result = null;
		return false;
	}
}
