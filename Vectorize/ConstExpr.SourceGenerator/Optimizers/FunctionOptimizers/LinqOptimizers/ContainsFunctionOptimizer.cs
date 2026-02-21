using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Contains context.Method.
/// Optimizes patterns such as:
/// - collection.Where(x => x == value).Any() => collection.Contains(value)
/// - collection.Select(...).Contains(value) => collection.Contains(...) (when projection is simple)
/// - collection.Distinct().Contains(value) => collection.Contains(value) (distinctness doesn't affect containment)
/// - collection.OrderBy(...).Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.OrderByDescending(...).Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.Order().Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.OrderDescending().Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.ThenBy(...).Contains(value) => collection.Contains(value) (secondary ordering doesn't affect containment)
/// - collection.ThenByDescending(...).Contains(value) => collection.Contains(value) (secondary ordering doesn't affect containment)
/// - collection.Reverse().Contains(value) => collection.Contains(value) (reversing doesn't affect containment)
/// - collection.AsEnumerable().Contains(value) => collection.Contains(value) (type cast doesn't affect containment)
/// - collection.ToList().Contains(value) => collection.Contains(value) (materialization doesn't affect containment)
/// - collection.ToArray().Contains(value) => collection.Contains(value) (materialization doesn't affect containment)
/// </summary>
public class ContainsFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Contains), 1, 2)
{
	// Operations that don't affect element containment (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectContainment =
	[
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if element exists, Contains is true
		nameof(Enumerable.OrderBy), // Ordering: changes order but not containment
		nameof(Enumerable.OrderByDescending), // Ordering: changes order but not containment
		"Order", // Ordering (.NET 6+): changes order but not containment
		"OrderDescending", // Ordering (.NET 6+): changes order but not containment
		nameof(Enumerable.ThenBy), // Secondary ordering: changes order but not containment
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not containment
		nameof(Enumerable.Reverse), // Reversal: changes order but not containment
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

		// Get the value to search for from Contains(value) or Contains(value, comparer)
		var searchValue = context.VisitedParameters[0];

		// Recursively skip all operations that don't affect containment
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectContainment, out source);

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
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectContainment, out invocationSource);

					// TODO: do this recursively for multiple chained Where statements
					if (searchValue is LiteralExpressionSyntax { Token.Value: { } literalValue }
					    && context.GetLambda(wherePredicate) is { } lambda)
					{
						switch (lambda.Compile().DynamicInvoke(literalValue))
						{
							case false:
							{
								result = SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
								return true;
							}
							case true:
							{
								TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectContainment, out source);
								break;
							}
						}
					}
					else
					{
						wherePredicate = context.Visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate;

						// Create a new lambda that combines the where predicate with equality check
						var lambdaParam = GetLambdaParameter(wherePredicate);
						var whereBody = GetLambdaBody(wherePredicate);
						var equalityCheck = SyntaxFactory.BinaryExpression(
							SyntaxKind.EqualsExpression,
							SyntaxFactory.IdentifierName(lambdaParam),
							searchValue);

						var combinedBody = SyntaxFactory.BinaryExpression(
							SyntaxKind.LogicalAndExpression,
							SyntaxFactory.ParenthesizedExpression(whereBody),
							SyntaxFactory.ParenthesizedExpression(equalityCheck));

						var anyPredicate = SyntaxFactory.SimpleLambdaExpression(
							SyntaxFactory.Parameter(SyntaxFactory.Identifier(lambdaParam)),
							combinedBody);

						// Use appropriate context.Method based on source type
						if (IsInvokedOnList(context.Model, invocationSource))
						{
							result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, "Exists", context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						if (IsInvokedOnArray(context.Model, invocationSource))
						{
							result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, nameof(Enumerable.Any), context.Visit(anyPredicate) ?? anyPredicate);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
				                                    && TryGetLambda(selectorArg, out var selector):
				{
					// Continue skipping operations before Select as well
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectContainment, out invocationSource);

					selector = context.Visit(selector) as LambdaExpressionSyntax ?? selector;

					// Try to convert to Any with equality check
					// selector is: x => x.Prop
					// searchValue is: value
					// Result should be: x => x.Prop == value
					if (TryGetLambdaBody(selector, out var selectorBody))
					{
						var lambdaParam = GetLambdaParameter(selector);
						var equalityCheck = SyntaxFactory.BinaryExpression(
							SyntaxKind.EqualsExpression,
							selectorBody,
							searchValue);

						var anyPredicate = SyntaxFactory.SimpleLambdaExpression(
							SyntaxFactory.Parameter(SyntaxFactory.Identifier(lambdaParam)),
							equalityCheck);

						// Use appropriate context.Method based on source type
						if (IsInvokedOnList(context.Model, invocationSource))
						{
							result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, "Exists", context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						if (IsInvokedOnArray(context.Model, invocationSource))
						{
							result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, nameof(Enumerable.Any), context.Visit(anyPredicate) ?? anyPredicate);
						return true;
					}

					break;
				}
			}

			// For List<T>, use the native Contains context.Method
			if (IsInvokedOnList(context.Model, source))
			{
				result = CreateInvocation(context.Visit(source) ?? source, "Contains", context.Visit(searchValue) ?? searchValue);
				return true;
			}

			// For arrays, use Array.IndexOf
			if (IsInvokedOnArray(context.Model, source))
			{
				var indexOfCall = CreateInvocation(
					SyntaxFactory.ParseTypeName(nameof(Array)),
					nameof(Array.IndexOf),
					context.Visit(source) ?? source,
					context.Visit(searchValue) ?? searchValue);

				result = SyntaxFactory.BinaryExpression(
					SyntaxKind.GreaterThanOrEqualExpression,
					indexOfCall,
					SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
				return true;
			}

			// If we skipped any operations, create optimized Contains() call
			if (isNewSource)
			{
				// Keep context.Parameters (including optional comparer)
				result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Contains), context.VisitedParameters);
				return true;
			}
		}

		// No matching chain found
		result = null;
		return false;
	}
}
