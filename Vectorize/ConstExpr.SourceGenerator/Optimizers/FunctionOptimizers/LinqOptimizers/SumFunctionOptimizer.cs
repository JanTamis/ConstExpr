using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
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
public class SumFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Sum), n => n is 0 or 1)
{
	// Operations that don't affect the sum
	private static readonly HashSet<string> OperationsThatDontAffectSum =
	[
		..MaterializingMethods,
		..OrderingOperations,
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
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
			result = CreateLiteral(0);
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

							result = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression, context.Visit(left) ?? left, context.Visit(constantValue) ?? constantValue, type);
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

					var leftInvocation = UpdateInvocation(context, methodSource);
					var rightInvocation = CreateInvocation(methodInvocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters);

					var left = TryOptimizeByOptimizer<SumFunctionOptimizer>(context, leftInvocation);
					var right = TryOptimizeByOptimizer<SumFunctionOptimizer>(context, rightInvocation);

					var sumType = context.Method.ReturnType;
					
					result = OptimizeArithmetic(context, SyntaxKind.AddExpression, context.Visit(left) ?? leftInvocation, context.Visit(right) ?? rightInvocation, sumType);
					return true;
				}
				case nameof(Enumerable.Range) when methodInvocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();

					// count * (2 * start + count - 1) / 2
					var twoTimesStart = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression,
						CreateLiteral(2), startArg.Expression, intType);

					// Shift operators (<<, >>) have lower precedence than + and -, so wrap in parens
					// to avoid `start << 1 + count` being parsed as `start << (1 + count)`.
					if (twoTimesStart is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LeftShiftExpression or (int)SyntaxKind.RightShiftExpression })
					{
						twoTimesStart = ParenthesizedExpression(twoTimesStart);
					}

					var twoStartPlusCount = OptimizeArithmetic(context, SyntaxKind.AddExpression,
						twoTimesStart, countArg.Expression, intType);

					var inner = ParenthesizedExpression(
						OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
							twoStartPlusCount, CreateLiteral(1), intType));

					var numerator = OptimizeArithmetic(context, SyntaxKind.MultiplyExpression,
						countArg.Expression, inner, intType);

					result = OptimizeArithmetic(context, SyntaxKind.DivideExpression,
						numerator, CreateLiteral(2), intType);
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
			var sum = values.Sum(context.Method.ReturnType);

			if (TryCreateLiteral(sum, out var sumLiteral))
			{
				result = sumLiteral;
				return true;
			}
		}

		if (IsEmptyEnumerable(source))
		{
			result = CreateLiteral(0);
			return true;
		}

		// If we skipped any operations, create optimized Sum() call
		if (isNewSource
		    || !AreEquivalent(source, newSource))
		{
			result = TryOptimizeAppend(context, newSource, UpdateInvocation(context, newSource));
			return true;
		}

		result = TryOptimizeAppend(context, newSource, context.Invocation);
		return !AreEquivalent(context.Invocation, result);
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
			var newInvocation = firstInvocation.WithExpression(memberAccess.WithExpression(source))
				.WithMethodSymbolAnnotation(context.Method, context.SymbolStore);

			if (TryExecutePredicates(context, source, out var optimizedResult, out _))
			{
				items[0] = context.Visit(optimizedResult) ?? optimizedResult as ExpressionSyntax ?? newInvocation;
			}
			else
			{
				items[0] = context.Visit(newInvocation) ?? newInvocation;
			}
		}

		var type = context.Method.ReturnType;

		// create add chain for all appended values: source.Sum() + appendedValue1 + appendedValue2 + ..., using aggregate to build the chain
		var sumExpression = items[0];

		foreach (var item in items.Skip(1))
		{
			sumExpression = OptimizeArithmetic(context, SyntaxKind.AddExpression, sumExpression!, item, type);
		}

		return sumExpression;
	}
}