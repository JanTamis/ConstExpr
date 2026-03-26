using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MathMaxOptimizer = ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MaxFunctionOptimizer;

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
		..MaterializingMethods,
		..OrderingOperations,
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect max
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMax, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize Max(x => x) => Max() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			result = UpdateInvocation(context, source, [ ]);
			return true;
		}

		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize source.Select(selector).Max() => source.Max(selector)
				case nameof(Enumerable.Select) when invocation.ArgumentList.Arguments.Count == 1:
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var selector = invocation.ArgumentList.Arguments[0].Expression;

					result = UpdateInvocation(context, invocationSource, selector);
					return true;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var concatArg = invocation.ArgumentList.Arguments[0].Expression;
					var visitedConcatArg = context.Visit(concatArg) ?? concatArg;

					var leftInvocation = UpdateInvocation(context, invocationSource);
					var rightInvocation = CreateInvocation(visitedConcatArg, Name, context.VisitedParameters);

					var left = TryOptimizeByOptimizer<MinFunctionOptimizer>(context, leftInvocation) ?? leftInvocation;
					var right = TryOptimizeByOptimizer<MinFunctionOptimizer>(context, rightInvocation) ?? rightInvocation;

					result = OptimizeAsMathPairwise<MathMaxOptimizer>(context, context.Visit(left) ?? leftInvocation, context.Visit(right) ?? rightInvocation);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();

					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType),
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, countArg.Expression, intType),
							CreateLiteral(1), intType),
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
						repeatElementArg.Expression,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
			}
		}

		// If we skipped any operations, create optimized Max() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}