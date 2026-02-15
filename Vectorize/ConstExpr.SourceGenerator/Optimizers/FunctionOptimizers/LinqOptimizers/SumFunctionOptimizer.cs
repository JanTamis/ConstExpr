using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Sum context.Method.
/// Optimizes patterns such as:
/// - collection.Sum(x => x) => collection.Sum() (identity lambda removal)
/// - collection.Select(x => x.Property).Sum() => collection.Sum(x => x.Property)
/// - collection.OrderBy(...).Sum() => collection.Sum() (ordering doesn't affect sum)
/// - collection.AsEnumerable().Sum() => collection.Sum()
/// - collection.ToList().Sum() => collection.Sum()
/// - collection.Reverse().Sum() => collection.Sum()
/// </summary>
public class SumFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Sum), 0, 1)
{
	// Operations that don't affect the sum
	private static readonly HashSet<string> OperationsThatDontAffectSum =
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

		// Recursively skip operations that don't affect sum
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSum, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}
		
		var newSource = context.Visit(source) ?? source;

		// Optimize Sum(x => x) => Sum() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(newSource, nameof(Enumerable.Sum));
			return true;
		}
		
		// Optimize source.Select(selector).Sum() => source.Sum(selector)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(newSource, nameof(Enumerable.Select), out var selectInvocation)
		    && TryGetLinqSource(selectInvocation, out var selectSource)
		    && selectInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectSum, out selectSource);
			
			var selector = selectInvocation.ArgumentList.Arguments[0].Expression;

			if (!TryGetLambda(selector, out var selectorLambda)
			    || !IsIdentityLambda(selectorLambda))
			{
				var visitedSelector = context.Visit(selector) ?? selector;
				result = CreateInvocation(selectSource, nameof(Enumerable.Sum), visitedSelector);
				return true;
			}
		}

		if (context.VisitedParameters.Count == 0
		    && TryGetValues(newSource, out var values)
		    && context.Method.ReceiverType is INamedTypeSymbol parameterType)
		{
			var sum = values.Sum(parameterType.TypeArguments[0]);

			if (SyntaxHelpers.TryGetLiteral(sum, out var sumLiteral))
			{
				result = sumLiteral;
				return true;
			}
		}

		// If we skipped any operations, create optimized Sum() call
		if (isNewSource
		    || !SyntaxFactory.AreEquivalent(source, newSource))
		{
			result = CreateInvocation(newSource, nameof(Enumerable.Sum), context.VisitedParameters);
			return true;
		}

		result = null;
		return false;
	}
}
