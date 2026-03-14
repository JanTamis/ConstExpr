using System.Collections.Generic;
using System.Linq;
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

		// Recursively skip operations that don't affect sum
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSum, out source);

		if (TryExecutePredicates(context, source, out result, out var newSource))
		{
			return true;
		}

		// Optimize Sum(x => x) => Sum() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			result = TryOptimizeAppend(context, newSource, CreateSimpleInvocation(newSource, nameof(Enumerable.Sum)));
			return true;
		}

		if (IsEmptyEnumerable(newSource))
		{
			result = SyntaxHelpers.CreateLiteral(0);
			return true;
		}

		// Optimize source.Select(selector).Sum() => source.Sum(selector)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(newSource, out var methodName, out var methodInvocation)
		    && TryGetLinqSource(methodInvocation, out var methodSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Select) when methodInvocation.ArgumentList.Arguments.Count == 1:
				{
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectSum, out methodSource);

					var selector = methodInvocation.ArgumentList.Arguments[0].Expression;

					if (TryGetLambda(selector, out var selectLambda))
					{
						if (IsConstantLambda(selectLambda, out var constantValue))
						{
							var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, methodSource), context.Method), out var leftResult)
								? leftResult as ExpressionSyntax
								: UpdateInvocation(context, methodSource);

							var type = context.Method.ReturnType;

							result = context.OptimizeBinaryExpression(SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, left!, constantValue), type, type, type);
							return true;
						}

						if (!IsIdentityLambda(selectLambda))
						{
							result = TryOptimizeAppend(context, methodSource, UpdateInvocation(context, methodSource, selector));
							return true;
						}
					}

					break;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectSum, out methodSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, methodSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(methodInvocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					result = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, left ?? CreateInvocation(methodSource, Name, context.VisitedParameters), right ?? CreateInvocation(methodInvocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters));
					return true;
				}
				case nameof(Enumerable.Range) when methodInvocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();

					// count * (2 * start + count - 1) / 2
					var twoTimesStart = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression,
						SyntaxHelpers.CreateLiteral(2)!, startArg.Expression, intType);

					var twoStartPlusCount = OptimizeArithmetic(context, SyntaxKind.AddExpression,
						twoTimesStart, countArg.Expression, intType);

					var inner = SyntaxFactory.ParenthesizedExpression(
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							twoStartPlusCount, SyntaxHelpers.CreateLiteral(1)!, intType));

					var numerator = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression,
						countArg.Expression, inner, intType);

					result = OptimizeArithmetic(context, SyntaxKind.DivideExpression,
						numerator, SyntaxHelpers.CreateLiteral(2)!, intType);
					return true;
				}
				case "Repeat" when methodInvocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					// Repeat(element, count).Sum() => element * count
					var elementType = context.Method.ReturnType;
					
					result = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression,
						repeatElementArg.Expression, repeatCountArg.Expression, elementType);
					return true;
				}
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

		if (IsEmptyEnumerable(source))
		{
			result = SyntaxHelpers.CreateLiteral(0);
			return true;
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
				case nameof(Enumerable.Prepend):
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

			if (TryExecutePredicates(context, source, out var optimizedResult, out _))
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