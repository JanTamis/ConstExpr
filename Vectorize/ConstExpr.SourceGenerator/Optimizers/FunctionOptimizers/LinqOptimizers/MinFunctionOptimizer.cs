using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MathMinOptimizer = ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MinFunctionOptimizer;

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

		// Recursively skip operations that don't affect min
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMin, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize Min(x => x) => Min() (identity lambda removal)
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
				// Optimize source.Select(selector).Min() => source.Min(selector)
				case nameof(Enumerable.Select) when invocation.ArgumentList.Arguments.Count == 1:
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMin, out invocationSource);

					var selector = invocation.ArgumentList.Arguments[0].Expression;

					result = UpdateInvocation(context, invocationSource, selector);
					return true;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMin, out invocationSource);

					var concatArg = invocation.ArgumentList.Arguments[0].Expression;
					var visitedConcatArg = context.Visit(concatArg) ?? concatArg;

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(visitedConcatArg, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var leftExpr = left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters);
					var rightExpr = right ?? CreateInvocation(visitedConcatArg, Name, context.VisitedParameters);

					result = OptimizeAsMathPairwise<MathMinOptimizer>(context, leftExpr, rightExpr);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						result = ConditionalExpression(
							OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
							startArg.Expression,
							CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
						return true;
					}

					break;
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

		// If we skipped any operations, create optimized Min() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}