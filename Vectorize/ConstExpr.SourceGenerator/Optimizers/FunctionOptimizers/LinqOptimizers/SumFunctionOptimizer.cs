using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;

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
		if (!IsValidLinqMethod(context)
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
			result = TryOptimizeAppend(context, newSource, CreateSimpleInvocation(newSource, nameof(Enumerable.Sum)));
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
				result = TryOptimizeAppend(context, selectSource, UpdateInvocation(context, selectSource, visitedSelector));
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
			result = TryOptimizeAppend(context, newSource, UpdateInvocation(context, newSource));
			return true;
		}

		result = TryOptimizeAppend(context, newSource, context.Invocation);
		return !SyntaxFactory.AreEquivalent(context.Invocation, result);
	}

	private ExpressionSyntax? TryOptimizeAppend(FunctionOptimizerContext context, ExpressionSyntax source, InvocationExpressionSyntax? result)
	{
		var items = new List<ExpressionSyntax>
		{
			result!,
		};

		while (IsLinqMethodChain(source, out var name, out var invocation))
		{
			switch (name)
			{
				case nameof(Enumerable.Append):
				{
					var appendedValue = invocation.ArgumentList.Arguments[0].Expression;
					var visitedAppendedValue = context.Visit(appendedValue) ?? appendedValue;

					items.Add(visitedAppendedValue);
					break;
				}
				case nameof(Enumerable.Concat) when TryGetSyntaxes(invocation.ArgumentList.Arguments[0].Expression, out var syntaxes):
				{
					items.AddRange(syntaxes.Select(s => context.Visit(s) ?? s));
					break;
				}
				default:
				{
					goto End;
				}
			}

			TryGetLinqSource(invocation, out source);

			TryGetOptimizedChainExpression(source, OperationsThatDontAffectSum, out source);
		}
		
		End:

		if (items[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } firstInvocation)
		{
			// update source of the Sum invocation to the final source after skipping Append chains
			var newInvocation = firstInvocation.WithExpression(memberAccess.WithExpression(source));

			if (TryExecutePredicates(context, source, out var optimizedResult))
			{
				items[0] = context.Visit(optimizedResult) ?? optimizedResult as ExpressionSyntax ?? newInvocation;
			}
			else
			{
				items[0] = newInvocation;
			}
		}

		var type = context.Method.ReturnType;

		// create add chain for all appended values: source.Sum() + appendedValue1 + appendedValue2 + ..., using aggregate to build the chain
		var sumExpression = items[0];

		foreach (var item in items.Skip(1))
		{
			sumExpression = context.OptimizeBinaryExpression(SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, sumExpression!, item), type, type, type) as ExpressionSyntax;
		}

		return sumExpression;
	}
}