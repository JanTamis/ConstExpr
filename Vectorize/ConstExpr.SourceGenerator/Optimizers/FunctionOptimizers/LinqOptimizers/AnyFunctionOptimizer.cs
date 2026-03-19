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
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Select), // Projection: transforms elements but doesn't filter
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if any exist, Any() is true
		nameof(Enumerable.GroupBy), // Grouping: groups elements but doesn't filter them out, so it doesn't affect whether any elements exist
		"Chunk", // Chunking: groups elements but doesn't filter them out, so it doesn't affect whether any elements exist
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

		if (TryExecutePredicates(context, source, out result, out source))
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
						predicate = CombinePredicates(predicate, anyPredicate);
					}

					if (IsSimpleEqualityLambda(predicate, out var equalityValue))
					{
						result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(invocationSource, nameof(Enumerable.Contains), equalityValue));
						return true;
					}

					if (IsInvokedOnList(context, invocationSource))
					{
						result = CreateInvocation(context.Visit(invocationSource) ?? invocationSource, "Exists", predicate);
						return true;
					}

					if (IsInvokedOnArray(context, invocationSource))
					{
						result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(invocationSource) ?? invocationSource, predicate);
						return true;
					}

					result = UpdateInvocation(context, invocationSource, predicate);
					return true;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectExistence, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var boolType = context.Model.Compilation.CreateBoolean();
					result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters), right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), boolType);
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
						=> OptimizeComparison(context, SyntaxKind.LogicalOrExpression, result, value, boolType));

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

							result = OptimizeComparison(context, SyntaxKind.LogicalOrExpression, left, right, boolType);
							return true;
						}
					}

					break;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();
						
						result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0)!, intType);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();

						// Repeat(element, count).Any() => count > 0
						result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0)!, intType);
						return true;
					}

					break;
				}
			}
		}

		if (context.VisitedParameters.Count == 0)
		{
			if (IsCollectionType(context, source))
			{
				var intType = context.Model.Compilation.CreateInt32();
				result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(source, "Count"),
					CreateLiteral(0)!, intType);

				return true;
			}

			if (IsInvokedOnArray(context, source))
			{
				var intType = context.Model.Compilation.CreateInt32();
				result = OptimizeComparison(context, SyntaxKind.GreaterThanExpression,
					CreateMemberAccess(source, "Length"),
					CreateLiteral(0)!, intType);

				return true;
			}
		}
		else if (TryGetLambda(context.VisitedParameters[0], out var anyLambda)
		         && IsSimpleEqualityLambda(anyLambda, out var equalityValue))
		{
			result = TryOptimizeByOptimizer<ContainsFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Contains), equalityValue));
			return true;
		}
		else if (context.VisitedParameters.Count == 1
		         && TryGetLambda(context.VisitedParameters[0], out anyLambda))
		{
			if (IsInvokedOnList(context, source))
			{
				result = CreateInvocation(context.Visit(source) ?? source, "Exists", anyLambda);
				return true;
			}

			if (IsInvokedOnArray(context, source))
			{
				result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.Exists), context.Visit(source) ?? source, anyLambda);
				return true;
			}

			result = UpdateInvocation(context, source, anyLambda);
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