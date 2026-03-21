using System;
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
/// Optimizer for Enumerable.All context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate1).All(predicate2) => collection.All(x => predicate1(x) && predicate2(x))
/// - collection.Select(...).All() => collection.All() (projection doesn't affect all-check)
/// - collection.Distinct().All() => collection.All() (distinctness doesn't affect all-check)
/// - collection.OrderBy(...).All() => collection.All() (ordering doesn't affect all-check)
/// - collection.OrderByDescending(...).All() => collection.All() (ordering doesn't affect all-check)
/// - collection.Order().All() => collection.All() (ordering doesn't affect all-check)
/// - collection.OrderDescending().All() => collection.All() (ordering doesn't affect all-check)
/// - collection.ThenBy(...).All() => collection.All() (secondary ordering doesn't affect all-check)
/// - collection.ThenByDescending(...).All() => collection.All() (secondary ordering doesn't affect all-check)
/// - collection.Reverse().All() => collection.All() (reversing doesn't affect all-check)
/// - collection.AsEnumerable().All() => collection.All() (type cast doesn't affect all-check)
/// - collection.ToList().All() => collection.All() (materialization doesn't affect all-check)
/// - collection.ToArray().All() => collection.All() (materialization doesn't affect all-check)
/// - Enumerable.Repeat(element, count).All(predicate) => count &lt;= 0 || predicate(element)
/// - Enumerable.Range(start, count).All(predicate) => count == 0 ? true : predicate(start) &amp;&amp; predicate(start + count - 1) (boundary check for monotone predicates, otherwise falls through)
/// </summary>
public class AllFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.All), 1)
{
	// Operations that don't affect the all-check (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectAll =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if all satisfy condition, All() is true
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Get the predicate from All(predicate)
		var allPredicate = GetMethodArguments(context.Invocation)
			.Select(s => s.Expression)
			.FirstOrDefault();

		if (!TryGetLambda(allPredicate, out var allLambda))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect all-check
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAll, out source);

		if (TryExecutePredicates(context, source, out result, out _))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Where) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					&& TryGetLambda(predicateArg, out var wherePredicate):
				{
					// Continue skipping operations before Where as well
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectAll, out source);

					allLambda = CombinePredicates(context.Visit(allLambda) as LambdaExpressionSyntax ?? allLambda, context.Visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate);
					isNewSource = true;
					break;
				}

				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectpredicateArg }
				                                    && TryGetLambda(selectpredicateArg, out var selectPredicate):
				{
					// Continue skipping operations before Select as well
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectAll, out source);

					allLambda = CombineSelectLambdas(context.Visit(allLambda) as LambdaExpressionSyntax ?? allLambda, context.Visit(selectPredicate) as LambdaExpressionSyntax ?? selectPredicate);
					isNewSource = true;
					break;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectAll, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var boolType = context.Model.Compilation.CreateBoolean();
					result = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters), right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), boolType);
					return true;
				}
				case nameof(Enumerable.Append) or nameof(Enumerable.Prepend):
				{
					if (context.VisitedParameters.Count == 0)
					{
						result = CreateLiteral(true);
						return true;
					}

					if (context.VisitedParameters.Count == 1
					    && TryGetLambda(context.VisitedParameters[0], out var anyPredicate)
					    && TryGetLambdaBody(anyPredicate, out var anyPredicateBody)
					    && TryGetSimpleLambdaParameter(anyPredicate, out var anyPredicateParam)
					    && TryGetElementType(context, out var elementType))
					{
						var boolType = context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean);

						// collect all the append arguments in case of multiple appends in a chain, e.g. source.Append(x).Append(y).Any()
						var appendValues = new List<ExpressionSyntax> { context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, invocation.ArgumentList.Arguments[0].Expression)) };

						while (IsLinqMethodChain(invocationSource, out methodName, out var currentMethodInvocation)
						       && methodName is nameof(Enumerable.Append) or nameof(Enumerable.Prepend)
						       && TryGetLinqSource(currentMethodInvocation, out invocationSource))
						{
							if (currentMethodInvocation.ArgumentList.Arguments.Count == 0)
							{
								result = CreateLiteral(true);
								return true;
							}

							appendValues.Add(context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, currentMethodInvocation.ArgumentList.Arguments[0].Expression)));
						}

						var updatedInvocation = UpdateInvocation(context, invocationSource);

						appendValues.Add(TryOptimize(context.WithInvocationAndMethod(updatedInvocation, context.Method), out var rightResult) ? rightResult as ExpressionSyntax : updatedInvocation);

						result = appendValues.Skip(1).Aggregate(appendValues[0], (result, value)
							=> OptimizeComparison(context, SyntaxKind.LogicalAndExpression, result, value, boolType));

						return true;
					}

					break;
				}
			case nameof(Enumerable.DefaultIfEmpty):
			{
				if (context.VisitedParameters.Count == 0)
				{
					result = CreateLiteral(true);
					return true;
				}

				if (TryGetElementType(context, out var elementType))
				{
					var defaultValue = invocation.ArgumentList.Arguments.Count == 0
						? elementType.GetDefaultValue()
						: invocation.ArgumentList.Arguments[0].Expression;

					if (context.VisitedParameters.Count == 1
					    && TryGetLambda(context.VisitedParameters[0], out var anyPredicate)
					    && TryGetLambdaBody(anyPredicate, out var anyPredicateBody)
					    && TryGetSimpleLambdaParameter(anyPredicate, out var anyPredicateParam))
					{
						var boolType = context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean);
						var updatedInvocation = UpdateInvocation(context, invocationSource);

						var left = context.Visit(ReplaceIdentifier(anyPredicateBody, anyPredicateParam.Identifier.Text, defaultValue)) ?? defaultValue;
						var right = TryOptimize(context.WithInvocationAndMethod(updatedInvocation, context.Method), out var rightResult) ? rightResult as ExpressionSyntax : updatedInvocation;

						result = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, left, right, boolType);
						return true;
					}
				}

				break;
			}
			case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [var repeatElementArg, var repeatCountArg]:
			{
				// Repeat(element, count).All(predicate) => count <= 0 || predicate(element)
				// All elements are identical, so the predicate only needs to hold for that single value.
				if (context.VisitedParameters.Count == 1
				    && TryGetLambda(context.VisitedParameters[0], out var repeatAllPredicate)
				    && TryGetLambdaBody(repeatAllPredicate, out var repeatAllPredicateBody)
				    && TryGetSimpleLambdaParameter(repeatAllPredicate, out var repeatAllPredicateParam))
				{
					var intType = context.Model.Compilation.CreateInt32();
					var boolType = context.Model.Compilation.CreateBoolean();

					var countCheck = OptimizeComparison(context, SyntaxKind.LessThanOrEqualExpression, repeatCountArg.Expression, CreateLiteral(0), intType);
					var predicateApplied = context.Visit(ReplaceIdentifier(repeatAllPredicateBody, repeatAllPredicateParam.Identifier.Text, repeatElementArg.Expression))
					                       ?? ReplaceIdentifier(repeatAllPredicateBody, repeatAllPredicateParam.Identifier.Text, repeatElementArg.Expression);

					result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, countCheck, predicateApplied, boolType);
					return true;
				}

				break;
			}
		}
		}
		
		if (IsInvokedOnArray(context, source))
		{
			result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.TrueForAll), source, context.Visit(allLambda) ?? allLambda);
			return true;
		}

		if (IsInvokedOnList(context, source))
		{
			result = CreateInvocation(source, "TrueForAll", context.Visit(allLambda) ?? allLambda);
			return true;
		}

		// If we skipped any operations, create optimized All() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source, context.Visit(allLambda) ?? allLambda);
			return true;
		}

		result = null;
		return false;
	}

	private LambdaExpressionSyntax CombineSelectLambdas(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// Replace the outer lambda's parameter with the inner lambda's body
		var combinedBody = ReplaceIdentifier(outerBody, outerParam, innerBody);

		// Create a new lambda with the inner parameter and the combined body
		return SimpleLambdaExpression(
			Parameter(Identifier(innerParam)),
			combinedBody
		);
	}
}