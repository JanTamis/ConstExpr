using System;
using System.Collections.Generic;
using System.Linq;
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
/// </summary>
public class AllFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.All), 1)
{
	// Operations that don't affect the all-check (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectAll =
	[
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if all satisfy condition, All() is true
		nameof(Enumerable.OrderBy), // Ordering: changes order but not all-check
		nameof(Enumerable.OrderByDescending), // Ordering: changes order but not all-check
		"Order", // Ordering (.NET 6+): changes order but not all-check
		"OrderDescending", // Ordering (.NET 6+): changes order but not all-check
		nameof(Enumerable.ThenBy), // Secondary ordering: changes order but not all-check
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not all-check
		nameof(Enumerable.Reverse), // Reversal: changes order but not all-check
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray), // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
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

		if (TryExecutePredicates(context, source, out result))
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
			}
		}

		if (IsInvokedOnArray(context.Model, source))
		{
			result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.TrueForAll), context.Visit(source) ?? source, context.Visit(allLambda) ?? allLambda);
			return true;
		}

		if (IsInvokedOnArray(context.Model, source))
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
		return SyntaxFactory.SimpleLambdaExpression(
			SyntaxFactory.Parameter(SyntaxFactory.Identifier(innerParam)),
			combinedBody
		);
	}
}