using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Any context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Any() => collection.Any(predicate)
/// - collection.Select(...).Any() => collection.Any() (projection doesn't affect existence)
/// - collection.Distinct().Any() => collection.Any() (distinctness doesn't affect existence)
/// - collection.OrderBy(...).Any() => collection.Any() (ordering doesn't affect existence)
/// - collection.OrderByDescending(...).Any() => collection.Any() (ordering doesn't affect existence)
/// - collection.Order().Any() => collection.Any() (ordering doesn't affect existence)
/// - collection.OrderDescending().Any() => collection.Any() (ordering doesn't affect existence)
/// - collection.ThenBy(...).Any() => collection.Any() (secondary ordering doesn't affect existence)
/// - collection.ThenByDescending(...).Any() => collection.Any() (secondary ordering doesn't affect existence)
/// - collection.Reverse().Any() => collection.Any() (reversing doesn't affect existence)
/// - collection.AsEnumerable().Any() => collection.Any() (type cast doesn't affect existence)
/// - collection.ToList().Any() => collection.Any() (materialization doesn't affect existence)
/// - collection.ToArray().Any() => collection.Any() (materialization doesn't affect existence)
/// </summary>
public class AnyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Any), 0, 1)
{
	// Operations that don't affect element existence (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectExistence =
	[
		nameof(Enumerable.Select), // Projection: transforms elements but doesn't filter
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if any exist, Any() is true
		nameof(Enumerable.OrderBy), // Ordering: changes order but not existence
		nameof(Enumerable.OrderByDescending), // Ordering: changes order but not existence
		"Order", // Ordering (.NET 6+): changes order but not existence
		"OrderDescending", // Ordering (.NET 6+): changes order but not existence
		nameof(Enumerable.ThenBy), // Secondary ordering: changes order but not existence
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not existence
		nameof(Enumerable.Reverse), // Reversal: changes order but not existence
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray), // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect existence
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectExistence, out source);

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
				                                   && TryGetLambda(predicateArg, out var predicate):
				{
					// Continue skipping operations before Where as well
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectExistence, out invocationSource);
					
					if (context.VisitedParameters.Count == 1 && TryGetLambda(context.VisitedParameters[0], out var anyPredicate))
					{
						predicate = CombinePredicates(context.Visit(predicate) as LambdaExpressionSyntax ?? predicate, context.Visit(anyPredicate) as LambdaExpressionSyntax ?? anyPredicate);
					}

					if (IsSimpleEqualityLambda(predicate, out var equalityValue))
					{
						result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(invocationSource, nameof(Enumerable.Contains), equalityValue));
						return true;
					}

					if (IsInvokedOnList(context, invocationSource))
					{
						result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, "Exists", context.Visit(predicate) ?? predicate);
						return true;
					}

					if (IsInvokedOnArray(context, invocationSource))
					{
						result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, context.Visit(predicate) ?? predicate);
						return true;
					}

					result = UpdateInvocation(context, invocationSource, context.Visit(predicate) ?? predicate);
					return true;
				}
			}
		}

		if (context.VisitedParameters.Count == 0)
		{
			if (IsCollectionType(context, source))
			{
				result = BinaryExpression(SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(context.Visit(source) ?? source, "Count"),
					LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

				return true;
			}

			if (IsInvokedOnArray(context, source))
			{
				result = BinaryExpression(SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(context.Visit(source) ?? source, "Length"),
					LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

				return true;
			}
		}
		else if (TryGetLambda(context.VisitedParameters[0], out var anyLambda)
		         && IsSimpleEqualityLambda(anyLambda, out var equalityValue))
		{
			result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Contains), equalityValue));
			return true;
		}

		// If we skipped any operations, create optimized Any() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}