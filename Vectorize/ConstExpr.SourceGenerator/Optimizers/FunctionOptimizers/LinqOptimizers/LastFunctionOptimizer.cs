using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Last method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Last() => collection.Last(predicate)
/// - collection.AsEnumerable().Last() => collection.Last() (type cast doesn't affect last)
/// - collection.ToList().Last() => collection.Last() (materialization doesn't affect last)
/// - collection.ToArray().Last() => collection.Last() (materialization doesn't affect last)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect which element is last, so we don't optimize those!
/// Note: Distinct might remove the last element if it's a duplicate, so we don't optimize that either!
/// </summary>
public class LastFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Last), 0, 1)
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

					result = TryOptimizeByOptimizer<LastFunctionOptimizer>(context, CreateInvocation(innerInvocation, nameof(Enumerable.Last), predicate));
					return true;
				}
				case nameof(Enumerable.Reverse):
				{
					result = TryOptimizeByOptimizer<FirstFunctionOptimizer>(context, CreateSimpleInvocation(methodSource, nameof(Enumerable.First)));
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
					     && TryGetLambda(predicateArg, out var predicate)
					     && context.Model.TryGetTypeSymbol(predicate, out var predicateType):
				{
					result = TryOptimizeByOptimizer<MaxByFunctionOptimizer>(context, CreateInvocation(methodSource, "MaxBy", predicate), context.Method.TypeArguments[0], predicateType);
					return true;
				}
				case nameof(Enumerable.OrderByDescending)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate)
					     && context.Model.TryGetTypeSymbol(predicate, out var predicateType):
				{
					result = TryOptimizeByOptimizer<MinByFunctionOptimizer>(context, CreateInvocation(methodSource, "MinBy", predicate), context.Method.TypeArguments[0], predicateType);
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
								var lastChunkSize = sourceSyntaxes.Count % chunkSizeValue;
								if (lastChunkSize == 0) lastChunkSize = chunkSizeValue;
								
								var elements = sourceSyntaxes
									.Skip(sourceSyntaxes.Count - lastChunkSize)
									.Select(ExpressionElement);

								result = CollectionExpression(
									SeparatedList<CollectionElementSyntax>(elements));
								return true;
							}
							
							// For arrays, we can directly index the first chunk: source[^chunkSize..]
							var prefix = IndexFromEndExpression(chunkSize);
							var rangeExpression = RangeExpression(prefix, null);
							
							result = CreateElementAccess(methodSource, rangeExpression);
							return true;
						}

						var takeInvocation = TryOptimizeByOptimizer<TakeFunctionOptimizer>(context, CreateInvocation(methodSource, nameof(Enumerable.Take), chunkSizeArg.Expression));

						result = TryOptimizeByOptimizer<ToArrayFunctionOptimizer>(context, CreateSimpleInvocation(takeInvocation as ExpressionSyntax, nameof(Enumerable.ToArray)));
						return true;
					}
					break;
				}
				case nameof(Enumerable.DefaultIfEmpty):
				{
					TryGetOptimizedChainExpression(methodSource, (HashSet<string>) [ nameof(Enumerable.DefaultIfEmpty) ], out methodSource);

					var defaultItem = invocation.ArgumentList.Arguments.Count == 0
						? context.Method.ReturnType is INamedTypeSymbol namedType ? namedType.GetDefaultValue() : LiteralExpression(SyntaxKind.DefaultLiteralExpression)
						: invocation.ArgumentList.Arguments[0].Expression;

					while (IsLinqMethodChain(source, nameof(Enumerable.DefaultIfEmpty), out var innerDefaultInvocation)
					       && TryGetLinqSource(innerDefaultInvocation, out var innerSource))
					{
						// Continue skipping operations before the inner DefaultIfEmpty
						TryGetOptimizedChainExpression(innerSource, MaterializingMethods, out source);

						defaultItem = innerDefaultInvocation.ArgumentList.Arguments
							.Select(s => s.Expression)
							.DefaultIfEmpty(LiteralExpression(SyntaxKind.DefaultLiteralExpression))
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
					var optimizedFirstResult = TryOptimizeByOptimizer<LastFunctionOptimizer>(context, newInvocation);

					result = ReplaceIdentifier(body, parameter.Identifier.Text, context.Visit(optimizedFirstResult) ?? optimizedFirstResult as ExpressionSyntax);
					return true;
				}
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					if (context.VisitedParameters.Count == 0)
					{
						var intType = context.Model.Compilation.CreateInt32();
						
						result = OptimizeArithmetic(context, SyntaxKind.SubtractExpression, 
							OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, countArg.Expression, intType), 
							CreateLiteral(1), intType);
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
		
		// For arrays, use direct array indexing: arr[^1]
		// For List<T>, use direct indexing: list[^1]
		if (context.Method.Parameters.Length is 0 or 1 
		    && (IsInvokedOnArray(context, source) || IsInvokedOnList(context, source)))
		{
			result = CreateElementAccess(source, PrefixUnaryExpression(
				SyntaxKind.IndexExpression, CreateLiteral(1)));
			return true;
		}
		
		// If we skipped any operations, create optimized Last() call
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
						Argument(PrefixUnaryExpression(
							SyntaxKind.IndexExpression,
							LiteralExpression(
								SyntaxKind.NumericLiteralExpression,
								Literal(1))))))),
			defaultItem);
	}
}


