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
	// Operations that don't affect which element is "last"
	// We CAN'T include ordering operations because they change which element comes last!
	// We CAN'T include Distinct because the last element might be a duplicate and get removed!
	private static readonly HashSet<string> OperationsThatDontAffectLast =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect which element is last
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectLast, out source);

		if (TryExecutePredicates(context, source, out result))
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
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectLast, out var innerInvocation);

					result = UpdateInvocation(context, innerInvocation, context.Visit(predicate) ?? predicate);
					return true;
				}
				case nameof(Enumerable.Reverse):
				{
					TryGetOptimizedChainExpression(methodSource, OperationsThatDontAffectLast, out var innerInvocation);

					result = CreateInvocation(context.Visit(innerInvocation) ?? innerInvocation, nameof(Enumerable.FirstOrDefault));
					return true;
				}
				case "Order":
				{
					result = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Max));
					return true;
				}
				case "OrderDescending":
				{
					result = CreateInvocation(context.Visit(methodSource) ?? methodSource, nameof(Enumerable.Min));
					return true;
				}
				case nameof(Enumerable.DefaultIfEmpty):
				{
					TryGetOptimizedChainExpression(methodSource, (HashSet<string>) [ nameof(Enumerable.DefaultIfEmpty) ], out methodSource);

					// optimize collection.DefaultIfEmpty() => collection.Length > 0 ? collection[0] : default
					var collection = context.Visit(methodSource) ?? methodSource;

					var defaultItem = invocation.ArgumentList.Arguments.Count == 0
						? context.Method.ReturnType is INamedTypeSymbol namedType ? namedType.GetDefaultValue() : SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)
						: context.Visit(invocation.ArgumentList.Arguments[0].Expression) ?? invocation.ArgumentList.Arguments[0].Expression;

					while (IsLinqMethodChain(source, nameof(Enumerable.DefaultIfEmpty), out var innerDefaultInvocation)
					       && TryGetLinqSource(innerDefaultInvocation, out var innerSource))
					{
						// Continue skipping operations before the inner DefaultIfEmpty
						TryGetOptimizedChainExpression(innerSource, OperationsThatDontAffectLast, out source);

						defaultItem = innerDefaultInvocation.ArgumentList.Arguments
							.Select(s => s.Expression)
							.DefaultIfEmpty(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))
							.First(); // Update default value to the last one to the last one

						isNewSource = true; // We effectively skipped an operation, so we have a new source to optimize from
					}

					if (IsInvokedOnArray(context.Model, methodSource))
					{
						result = CreateDefaultIfEmptyConditional(collection, "Length", defaultItem);
						return true;
					}

					if (IsCollectionType(context.Model, methodSource))
					{
						result = CreateDefaultIfEmptyConditional(collection, "Count", defaultItem);
						return true;
					}

					break;
				}
				case nameof(Enumerable.Append) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } appendArg }:
				{
					result = appendArg;
					return true;
				}
			}
		}
		
		// For arrays, use conditional: arr.Length > 0 ? arr[^1] : default
		if (IsInvokedOnArray(context.Model, source))
		{
			source = context.Visit(source) ?? source;
			
			result = CreateDefaultIfEmptyConditional(source, "Length", SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression));
			return true;
		}

		// For List<T>, use conditional: list.Count > 0 ? list[^1] : default
		if (IsInvokedOnList(context.Model, source))
		{
			source = context.Visit(source) ?? source;
			
			result = CreateDefaultIfEmptyConditional(source, "Count", SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression));
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

	private SyntaxNode CreateDefaultIfEmptyConditional(ExpressionSyntax collection, string propertyName, ExpressionSyntax defaultItem)
	{
		return SyntaxFactory.ConditionalExpression(
			SyntaxFactory.BinaryExpression(
				SyntaxKind.GreaterThanExpression,
				CreateMemberAccess(collection, propertyName),
				SyntaxHelpers.CreateLiteral(0)!), CreateElementAccess(collection, SyntaxFactory.PrefixUnaryExpression(
				SyntaxKind.IndexExpression, SyntaxHelpers.CreateLiteral(1)!)),
			defaultItem);
	}
}


