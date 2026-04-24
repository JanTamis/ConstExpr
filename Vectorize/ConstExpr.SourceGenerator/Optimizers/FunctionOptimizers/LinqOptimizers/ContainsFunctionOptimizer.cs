using System;
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
public class ContainsFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Contains), n => n is 1 or 2)
{
	// Operations that don't affect element containment (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectContainment =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Distinct) // Deduplication: may reduce count, but if element exists, Contains is true
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Get the value to search for from Contains(value) or Contains(value, comparer)
		var searchValue = context.VisitedParameters[0];

		// Recursively skip all operations that don't affect containment
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectContainment, out source);

		if (TryExecutePredicates(context, source, out result, out _))
		{
			return true;
		}

		while (IsLinqMethodChain(source, out var methodName, out var invocation)
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
								result = LiteralExpression(SyntaxKind.FalseLiteralExpression);
								return true;
							}
							case true:
							{
								TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectContainment, out source);
								isNewSource = true;
								break;
							}
						}
					}
					else
					{
						wherePredicate = context.Visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate;

						var boolType = context.Model.Compilation.CreateBoolean();

						// Create a new lambda that combines the where predicate with equality check
						var lambdaParam = GetLambdaParameter(wherePredicate);
						var whereBody = GetLambdaBody(wherePredicate);
						var equalityCheck = OptimizeComparison(context, SyntaxKind.EqualsExpression,
							IdentifierName(lambdaParam),
							searchValue, boolType);

						var combinedBody = OptimizeComparison(context, SyntaxKind.LogicalAndExpression,
							ParenthesizedExpression(whereBody),
							ParenthesizedExpression(equalityCheck), boolType);

						var anyPredicate = SimpleLambdaExpression(
							Parameter(Identifier(lambdaParam)),
							combinedBody);

						// Use appropriate context.Method based on source type
						if (IsInvokedOnList(context, invocationSource))
						{
							result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, "Exists", context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						if (IsInvokedOnArray(context, invocationSource))
						{
							result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, context.Visit(anyPredicate) ?? anyPredicate);
							return true;
						}

						result = TryOptimizeByOptimizer<AnyFunctionOptimizer>(context, CreateInvocation(invocationSource, nameof(Enumerable.Any), anyPredicate));
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
						var equalityCheck = OptimizeComparison(context, SyntaxKind.EqualsExpression,
							selectorBody,
							searchValue, context.Model.Compilation.CreateBoolean());

						var anyPredicate = SimpleLambdaExpression(
							Parameter(Identifier(lambdaParam)),
							equalityCheck);

						var resultPredicate = context.Visit(anyPredicate) as LambdaExpressionSyntax ?? anyPredicate;

						if (IsInvokedOnArray(context, invocationSource))
						{
							if (IsSimpleEqualityLambda(resultPredicate, out var value))
							{
								var indexOfCall = CreateInvocation(
									ParseTypeName(nameof(Array)),
									nameof(Array.IndexOf),
									context.Visit(invocationSource) ?? invocationSource,
									value);

								result = OptimizeComparison(context, SyntaxKind.GreaterThanOrEqualExpression,
									indexOfCall,
									LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),
									context.Model.Compilation.GetSpecialType(SpecialType.System_Int32));
								return true;
							}

							result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, resultPredicate);
							return true;
						}

						if (IsSimpleEqualityLambda(resultPredicate, out var resultValue))
						{
							result = UpdateInvocation(context, invocationSource, resultValue);
							return true;
						}

						invocationSource = context.Visit(invocationSource) ?? invocationSource;

						// Use appropriate context.Method based on source type
						if (IsInvokedOnList(context, invocationSource))
						{
							result = CreateInvocation(invocationSource, "Exists", resultPredicate);
							return true;
						}

						result = CreateInvocation(invocationSource, nameof(Enumerable.Any), resultPredicate);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectContainment, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var boolType = context.Model.Compilation.CreateBoolean();
					result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters), right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), boolType);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					var intType = context.Model.Compilation.CreateInt32();

					var left = OptimizeComparison(context, SyntaxKind.GreaterThanOrEqualExpression, searchValue, startArg.Expression, intType);
					var right = OptimizeComparison(context, SyntaxKind.LessThanExpression, searchValue,
						OptimizeArithmetic(context, SyntaxKind.AddExpression, countArg.Expression, startArg.Expression, intType), intType);

					// searchValue >= start && searchValue < count + start
					result = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, left, right, context.Model.Compilation.CreateBoolean());
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					var boolType = context.Model.Compilation.CreateBoolean();
					var intType = context.Model.Compilation.CreateInt32();

					// Repeat(element, count).Contains(x) => count > 0 && element == x
					var countPositive = OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), intType);
					var elementEquals = OptimizeComparison(context, SyntaxKind.EqualsExpression, repeatElementArg.Expression, searchValue, intType);

					result = OptimizeComparison(context, SyntaxKind.LogicalAndExpression, countPositive, elementEquals, boolType);
					return true;
				}
			}
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Literal collection: [1, 2, 3].Contains(x) → x is 1 or 2 or 3
		// This is more efficient than Array.IndexOf when the collection is a small constant set.
		const int maxIsPatternElements = 8;

		if (TryGetSyntaxes(source, out var litSyntaxes)
		    && litSyntaxes.Count is > 0 and <= maxIsPatternElements
		    && litSyntaxes.All(s => s is LiteralExpressionSyntax)
		    && searchValue is not LiteralExpressionSyntax)
		{
			var orPattern = litSyntaxes
				.Select(PatternSyntax (syntax) => ConstantPattern(syntax))
				.Aggregate((left, right) => BinaryPattern(SyntaxKind.OrPattern, left, right));

			result = context.Visit(IsPatternExpression(searchValue, orPattern));
			return true;
		}

		// For arrays, use Array.IndexOf
		if (IsInvokedOnArray(context, source))
		{
			var indexOfCall = CreateInvocation(
				ParseTypeName(nameof(Array)),
				nameof(Array.IndexOf),
				source,
				searchValue);

			result = OptimizeComparison(context, SyntaxKind.GreaterThanOrEqualExpression,
				indexOfCall,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),
				context.Model.Compilation.GetSpecialType(SpecialType.System_Int32));
			return true;
		}

		// If we skipped any operations, create optimized Contains() call
		if (isNewSource)
		{
			// Keep context.Parameters (including optional comparer)
			result = UpdateInvocation(context, source);
			return true;
		}

		// No matching chain found
		result = null;
		return false;
	}
}