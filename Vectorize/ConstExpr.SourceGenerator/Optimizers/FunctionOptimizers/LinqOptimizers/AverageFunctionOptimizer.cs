using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Average context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().Average() =&gt; collection.Average() (skip type cast)
/// - collection.ToList().Average() =&gt; collection.Average() (skip materialization)
/// - collection.ToArray().Average() =&gt; collection.Average() (skip materialization)
/// - collection.OrderBy(...).Average() =&gt; collection.Average() (ordering doesn't affect average)
/// - collection.Reverse().Average() =&gt; collection.Average() (reversing doesn't affect average)
/// </summary>
public class AverageFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Average), n => n is 0 or 1)
{
	// Operations that don't affect Average behavior
	private static readonly HashSet<string> OperationsThatDontAffectAverage =
	[
		..MaterializingMethods,
		..OrderingOperations
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAverage, out source);

		if (IsEmptyEnumerable(source))
		{
			// Average of an empty sequence throws an exception, so we return a throw expression instead of optimizing to Average() which would be incorrect
			result = CreateThrowExpression<InvalidOperationException>("Sequence contains no elements");
			return true;
		}

		// check for x.Average(a => a) pattern and optimize to x.Average() since the selector is just the identity function and doesn't affect the average result
		if (context.VisitedParameters.Count > 0
		    && TryGetLambda(context.VisitedParameters[0], out var selector)
		    && IsIdentityLambda(selector))
		{
			result = UpdateInvocation(context, source, [ ]);
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
				                                    && TryGetLambda(selectorArg, out selector):
				{
					result = UpdateInvocation(context, invocationSource, selector);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();
					var doubleType = context.Model.Compilation.CreateDouble();

					// start + (count - 1) / 2.0
					var countMinusOne = ParenthesizedExpression(
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							countArg.Expression, CreateLiteral(1), intType));

					var halfOffset = OptimizeArithmetic(context, SyntaxKind.DivideExpression,
						countMinusOne, CreateLiteral(2.0), doubleType);

					var averageValue = OptimizeArithmetic(context, SyntaxKind.AddExpression,
						startArg.Expression, halfOffset, doubleType);

					// Guard against empty range (count == 0)
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType),
						averageValue,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					// Repeat(element, count).Average() => count > 0 ? (T)element : throw
					var castExpr = CastExpression(
						ParseName(context.Model.Compilation.GetMinimalString(context.Method.TypeArguments[0])),
						repeatElementArg.Expression);

					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
						castExpr,
						CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
					return true;
				}
			}
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}