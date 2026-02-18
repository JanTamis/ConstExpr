using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
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
public class FirstFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.First), 0, 1)
{
	// Operations that don't affect which element is "first"
	// We CAN'T include ordering operations because they change which element comes first!
	private static readonly HashSet<string> OperationsThatDontAffectFirst =
	[
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray), // Materialization: preserves order and all elements
		nameof(Enumerable.Take), // Taking more elements doesn't change which one is first
		nameof(Enumerable.Distinct), // Distinct might remove duplicates but doesn't change the order of remaining elements
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect which element is first
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectFirst, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		var visitedSource = context.Visit(source) ?? source;

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var methodSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Where)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var predicate):
				{
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectFirst, out var innerInvocation);

					result = CreateInvocation(context.Visit(innerInvocation) ?? innerInvocation, nameof(Enumerable.First), context.Visit(predicate) ?? predicate);
					return true;
				}
				case nameof(Enumerable.Reverse):
				{
					result = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Last));
					return true;
				}
				case "Order":
				{
					result = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Min));
					return true;
				}
				case "OrderDescending":
				{
					result = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Max));
					return true;
				}
				case "Chunk" when invocation.ArgumentList.Arguments is [ var chunkSizeArg ]:
				{
					var chunkSize = context.Visit(chunkSizeArg.Expression) ?? chunkSizeArg.Expression;

					if (chunkSize is LiteralExpressionSyntax { Token.Value: 1 })
					{
						source = methodSource;
					}
					else
					{
						if (IsInvokedOnArray(context.Model, methodSource))
						{
							// For arrays, we can directly index the first chunk: source[..chunkSize]
							result = SyntaxFactory.ElementAccessExpression(
								context.Visit(methodSource) ?? methodSource,
								SyntaxFactory.BracketedArgumentList(
									SyntaxFactory.SingletonSeparatedList(
										SyntaxFactory.Argument(
											SyntaxFactory.ParseExpression($"..{chunkSize}")))));
							return true;
						}
						
						var takeInvocation = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Take), chunkSize);
						
						result = CreateInvocation(takeInvocation, nameof(Enumerable.ToArray));
						return true;
					}
					break;
				}
			}
		}

		// For arrays, use direct array indexing: arr[0]
		// For List<T>, use direct indexing: list[0]
		if (IsInvokedOnArray(context.Model, source)
		    || IsInvokedOnList(context.Model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				context.Visit(source) ?? source,
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.NumericLiteralExpression,
								SyntaxFactory.Literal(0))))));
			return true;
		}

		// For arrays, use direct array indexing: arr[0]
		// For List<T>, use direct indexing: list[0]
		if (IsInvokedOnArray(context.Model, visitedSource)
		    || IsInvokedOnList(context.Model, visitedSource))
		{
			result = SyntaxFactory.ElementAccessExpression(
				visitedSource,
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.NumericLiteralExpression,
								SyntaxFactory.Literal(0))))));
			return true;
		}

		// If we skipped any operations, create optimized First() call
		if (isNewSource)
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.First));
			return true;
		}

		result = null;
		return false;
	}
}