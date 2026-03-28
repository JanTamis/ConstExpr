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
/// Optimizer for Enumerable.LastOrDefault context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).LastOrDefault() => collection.LastOrDefault(predicate)
/// - collection.AsEnumerable().LastOrDefault() => collection.LastOrDefault() (type cast doesn't affect last)
/// - collection.ToList().LastOrDefault() => collection.LastOrDefault() (materialization doesn't affect last)
/// - collection.ToArray().LastOrDefault() => collection.LastOrDefault() (materialization doesn't affect last)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect which element is last, so we don't optimize those!
/// Note: Distinct might remove the last element if it's a duplicate, so we don't optimize that either!
/// </summary>
public class LastOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.LastOrDefault), 0, 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip all operations that don't affect which element is last
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var methodSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Where)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate):
				{
					TryGetOptimizedChainExpression(methodSource, MaterializingMethods, out var innerInvocation);

					result = TryOptimizeByOptimizer<LastOrDefaultFunctionOptimizer>(context, CreateInvocation(innerInvocation, nameof(Enumerable.LastOrDefault), predicate));
					return true;
				}
				case nameof(Enumerable.Reverse):
				{
					TryGetOptimizedChainExpression(methodSource, MaterializingMethods, out var innerInvocation);

					result = TryOptimizeByOptimizer<FirstOrDefaultFunctionOptimizer>(context, CreateSimpleInvocation(innerInvocation, nameof(Enumerable.FirstOrDefault)));
					return true;
				}
				case "Order":
				{
					result = TryOptimizeByOptimizer<MaxFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Max)));
					return true;
				}
				case "OrderDescending":
				{
					result = TryOptimizeByOptimizer<MinFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Min)));
					return true;
				}
				case nameof(Enumerable.OrderBy)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate):
				{
					if (IsIdentityLambda(predicate))
					{
						goto case "Order";
					}

					result = TryOptimizeByOptimizer<MaxByFunctionOptimizer>(context, CreateInvocation(methodSource, "MaxBy", predicate));
					return true;
				}
				case nameof(Enumerable.OrderByDescending)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate):
				{
					if (IsIdentityLambda(predicate))
					{
						goto case "OrderDescending";
					}

					result = TryOptimizeByOptimizer<MinByFunctionOptimizer>(context, CreateInvocation(methodSource, "MinBy", predicate));
					return true;
				}
				case nameof(Enumerable.DefaultIfEmpty):
				{
					TryGetOptimizedChainExpression(methodSource, (HashSet<string>) [ nameof(Enumerable.DefaultIfEmpty) ], out methodSource);

					var defaultItem = invocation.ArgumentList.Arguments.Count == 0
						? context.Method.ReturnType.GetDefaultValue()
						: invocation.ArgumentList.Arguments[0].Expression;

					while (IsLinqMethodChain(source, nameof(Enumerable.DefaultIfEmpty), out var innerDefaultInvocation)
					       && TryGetLinqSource(innerDefaultInvocation, out var innerSource))
					{
						// Continue skipping operations before the inner DefaultIfEmpty
						TryGetOptimizedChainExpression(innerSource, MaterializingMethods, out source);

						defaultItem = innerDefaultInvocation.ArgumentList.Arguments
							.Select(s => s.Expression)
							.DefaultIfEmpty(context.Method.ReturnType.GetDefaultValue())
							.First(); // Update default value to the last one to the last one

						isNewSource = true; // We effectively skipped an operation, so we have a new source to optimize from
					}

					if (IsInvokedOnArray(context, methodSource))
					{
						result = CreateDefaultIfEmptyConditional(context, methodSource, "Length", defaultItem);
						return true;
					}

					if (IsCollectionType(context, methodSource))
					{
						result = CreateDefaultIfEmptyConditional(context, methodSource, "Count", defaultItem);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Append) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } appendArg }:
				{
					result = appendArg;
					return true;
				}
				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
				                                    && TryGetLambda(selectorArg, out var selector)
				                                    && TryGetSimpleLambdaParameter(selector, out var parameter)
				                                    && TryGetLambdaBody(selector, out var body):
				{
					var newInvocation = UpdateInvocation(context, methodSource);

					var optimizedFirst = TryOptimize(context.WithInvocationAndMethod(newInvocation, context.Method), out var optimizedFirstResult)
						? optimizedFirstResult
						: newInvocation;

					result = ReplaceIdentifier(body, parameter.Identifier.Text, optimizedFirst as ExpressionSyntax);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();

						result = ConditionalExpression(
							OptimizeComparison(context, SyntaxKind.GreaterThanExpression, countArg.Expression, CreateLiteral(0), intType),
							OptimizeArithmetic(context, SyntaxKind.SubtractExpression,
								OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, countArg.Expression, intType),
								CreateLiteral(1), intType),
							context.Method.TypeArguments[0].GetDefaultValue());
						return true;
					}

					break;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						// Repeat(element, count).FirstOrDefault() => count > 0 ? element : default
						result = ConditionalExpression(
							OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
							repeatElementArg.Expression,
							context.Method.TypeArguments[0].GetDefaultValue());
						return true;
					}

					break;
				}
			}
		}

		if (IsInvokedOnArray(context, source))
		{
			if (context.Method.Parameters.Length == 2)
			{
				result = CreateInvocation(ParseTypeName(nameof(Array)), nameof(Array.FindLast), source, context.VisitedParameters[0]);
			}
			else
			{
				// For arrays, use conditional: arr.Length > 0 ? arr[^1] : default
				result = CreateDefaultIfEmptyConditional(context, source, "Length", context.Method.ReturnType.GetDefaultValue());
			}

			return true;
		}

		if (IsInvokedOnList(context, source))
		{
			if (context.Method.Parameters.Length == 2)
			{
				result = CreateInvocation(source, "FindLast", context.VisitedParameters[0]);
			}
			else
			{
				// For List<T>, use conditional: list.Count > 0 ? list[^1] : default
				result = CreateDefaultIfEmptyConditional(context, source, "Count", context.Method.ReturnType.GetDefaultValue());
			}

			return true;
		}
		
		// If we skipped any operations, create optimized LastOrDefault() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	private SyntaxNode CreateDefaultIfEmptyConditional(FunctionOptimizerContext context, ExpressionSyntax collection, string propertyName, ExpressionSyntax defaultItem)
	{
		var intType = context.Model.Compilation.CreateInt32();

		return ConditionalExpression(
			OptimizeComparison(context, SyntaxKind.GreaterThanExpression,
				CreateMemberAccess(collection, propertyName),
				CreateLiteral(0), intType), CreateElementAccess(collection, PrefixUnaryExpression(
				SyntaxKind.IndexExpression, CreateLiteral(1))),
			defaultItem);
	}
}


