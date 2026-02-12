using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var predicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectLast, out whereSource);
			
			result = CreateInvocation(context.Visit(whereSource) ?? whereSource, nameof(Enumerable.Last), context.Visit(predicate) ?? predicate);
			return true;
		}
		
		// now check if we have a Reverse at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Reverse), out var reverseInvocation)
		    && TryGetLinqSource(reverseInvocation, out var reverseSource))
		{
			result = CreateInvocation(context.Visit(reverseSource) ?? reverseSource, nameof(Enumerable.First));
			return true;
		}
		
		// now check if we have a Order at the end of the optimized chain
		if (IsLinqMethodChain(source, "Order", out var orderInvocation)
		    && TryGetLinqSource(orderInvocation, out var orderSource))
		{
			result = CreateInvocation(context.Visit(orderSource) ?? orderSource, nameof(Enumerable.Max));
			return true;
		}
		
		// now check if we have a OrderDescending at the end of the optimized chain
		if (IsLinqMethodChain(source, "OrderDescending", out var orderDescInvocation)
		    && TryGetLinqSource(orderDescInvocation, out var orderDescSource))
		{
			result = CreateInvocation(context.Visit(orderDescSource) ?? orderDescSource, nameof(Enumerable.Min));
			return true;
		}
		
		// For arrays, use direct array indexing: arr[^1]
		// For List<T>, use direct indexing: list[^1]
		if (IsInvokedOnArray(context.Model, source) || IsInvokedOnList(context.Model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				context.Visit(source) ?? source,
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(
							SyntaxFactory.PrefixUnaryExpression(
								SyntaxKind.IndexExpression,
								SyntaxFactory.LiteralExpression(
									SyntaxKind.NumericLiteralExpression,
									SyntaxFactory.Literal(1)))))));
			return true;
		}
		
		// If we skipped any operations, create optimized Last() call
		if (isNewSource)
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Last));
			return true;
		}

		result = null;
		return false;
	}
}


