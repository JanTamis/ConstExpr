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
/// Optimizer for Enumerable.First context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).First() => collection.First(predicate)
/// - collection.AsEnumerable().First() => collection.First() (type cast doesn't affect first)
/// - collection.ToList().First() => collection.First() (materialization doesn't affect first)
/// - collection.ToArray().First() => collection.First() (materialization doesn't affect first)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect which element is first, so we don't optimize those!
/// Note: Distinct might remove the first element if it's a duplicate, so we don't optimize that either!
/// </summary>
public class FirstFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.First), n => n is 0 or 1)
{
	// Operations that don't affect which element is "first"
	// We CAN'T include ordering operations because they change which element comes first!
	private static readonly HashSet<string> OperationsThatDontAffectFirst =
	[
		..MaterializingMethods,
		nameof(Enumerable.Distinct) // Distinct might remove duplicates but doesn't change the order of remaining elements
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Recursively skip all operations that don't affect which element is first
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectFirst, out source);

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
					result = TryOptimizeByOptimizer<FirstFunctionOptimizer>(context, CreateInvocation(methodSource, nameof(Enumerable.First), predicate));
					return true;
				}
				case nameof(Enumerable.Reverse):
				{
					result = TryOptimizeByOptimizer<LastFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Last)));
					return true;
				}
				case "Order":
				{
					result = TryOptimizeByOptimizer<MinFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Min)));
					return true;
				}
				case "OrderDescending":
				{
					result = TryOptimizeByOptimizer<MaxFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.Max)));
					return true;
				}
				case nameof(Enumerable.OrderBy)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate)
					     && context.Model.TryGetTypeSymbol(predicate, context.SymbolStore, out var predicateType):
				{
					result = TryOptimizeByOptimizer<MinByFunctionOptimizer>(context, CreateInvocation(methodSource, "MinBy", predicate), context.Method.TypeArguments[0], predicateType);
					return true;
				}
				case nameof(Enumerable.OrderByDescending)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate)
					     && context.Model.TryGetTypeSymbol(predicate, context.SymbolStore, out var predicateType):
				{
					result = TryOptimizeByOptimizer<MaxByFunctionOptimizer>(context, CreateInvocation(methodSource, "MaxBy", predicate), context.Method.TypeArguments[0], predicateType);
					return true;
				}
				case "Chunk" when invocation.ArgumentList.Arguments is [ var chunkSizeArg ]:
				{
					var chunkSize = chunkSizeArg.Expression;

					if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
					{
						source = methodSource;
					}
					else
					{
						if (IsInvokedOnArray(context, methodSource))
						{
							if (TryGetSyntaxes(methodSource, out var sourceSyntaxes)
							    && chunkSize is LiteralExpressionSyntax { Token.Value: int chunkSizeValue })
							{
								var elements = sourceSyntaxes
									.Take(chunkSizeValue)
									.Select(ExpressionElement);

								result = CollectionExpression(
									SeparatedList<CollectionElementSyntax>(elements));
								return true;
							}

							// For arrays, we can directly index the first chunk: source[..chunkSize]
							result = CreateElementAccess(methodSource, ParseExpression($"..{chunkSize}"));
							return true;
						}

						var takeInvocation = TryOptimizeByOptimizer<TakeFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Take), chunkSizeArg.Expression));

						result = TryOptimizeByOptimizer<ToArrayFunctionOptimizer>(context, CreateSimpleInvocation(takeInvocation as ExpressionSyntax, nameof(Enumerable.ToArray)));
						return true;
					}
					break;
				}
				case nameof(Enumerable.DefaultIfEmpty):
				{
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectFirst.Union([ nameof(Enumerable.DefaultIfEmpty) ]).ToSet(), out methodSource);

					var defaultItem = invocation.ArgumentList.Arguments.Count == 0
						? context.Method.ReturnType is INamedTypeSymbol namedType ? namedType.GetDefaultValue() : LiteralExpression(SyntaxKind.DefaultLiteralExpression)
						: invocation.ArgumentList.Arguments[0].Expression;

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

					// result = CreateInvocation(methodSource, nameof(Enumerable.DefaultIfEmpty), defaultItem);
					// return true;
					break;
				}
				case nameof(Enumerable.Prepend) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } appendArg }:
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
					var optimizedFirstResult = TryOptimizeByOptimizer<FirstFunctionOptimizer>(context, newInvocation);

					result = ReplaceIdentifier(body, parameter.Identifier.Text, context.Visit(optimizedFirstResult) ?? optimizedFirstResult as ExpressionSyntax);
					return true;
				}
				case nameof(Enumerable.Skip) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } skipArg }:
				{
					result = TryOptimizeByOptimizer<ElementAtFunctionOptimizer>(context, CreateInvocation(methodSource, nameof(Enumerable.ElementAt), skipArg));
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
						result = startArg.Expression;
						return true;
					}

					break;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						// Repeat(element, count).FirstOrDefault() => count > 0 ? element : throw exception
						result = ConditionalExpression(
							OptimizeComparison(context, SyntaxKind.GreaterThanExpression, repeatCountArg.Expression, CreateLiteral(0), context.Model.Compilation.CreateInt32()),
							repeatElementArg.Expression,
							CreateThrowExpression<InvalidOperationException>("Sequence contains no elements"));
						return true;
					}

					break;
				}
			}
		}

		// For arrays, use direct array indexing: arr[0]
		// For List<T>, use direct indexing: list[0]
		if (context.VisitedParameters.Count == 0
		    && (IsInvokedOnArray(context, source)
		        || IsInvokedOnList(context, source)))
		{
			result = CreateElementAccess(source, CreateLiteral(0)!);
			return true;
		}

		// If we skipped any operations, create optimized First() call
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
				CreateLiteral(0), intType),
			ElementAccessExpression(
				collection,
				BracketedArgumentList(
					SingletonSeparatedList(
						Argument(CreateLiteral(0))))),
			defaultItem);
	}
}