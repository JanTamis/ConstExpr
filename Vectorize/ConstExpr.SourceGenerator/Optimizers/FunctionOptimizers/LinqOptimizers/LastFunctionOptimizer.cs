using System;
using System.Collections.Generic;
using System.Linq;
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
public class LastFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Last), 0)
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

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect which element is last
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectLast, out source);

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var predicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectLast, out whereSource);
			
			result = CreateInvocation(visit(whereSource) ?? whereSource, nameof(Enumerable.Last), visit(predicate) ?? predicate);
			return true;
		}
		
		// now check if we have a Reverse at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Reverse), out var reverseInvocation)
		    && TryGetLinqSource(reverseInvocation, out var reverseSource))
		{
			result = CreateInvocation(visit(reverseSource) ?? reverseSource, nameof(Enumerable.First));
			return true;
		}
		
		// now check if we have a Order at the end of the optimized chain
		if (IsLinqMethodChain(source, "Order", out var orderInvocation)
		    && TryGetLinqSource(orderInvocation, out var orderSource))
		{
			result = CreateInvocation(visit(orderSource) ?? orderSource, nameof(Enumerable.Max));
			return true;
		}
		
		// now check if we have a OrderDescending at the end of the optimized chain
		if (IsLinqMethodChain(source, "OrderDescending", out var orderDescInvocation)
		    && TryGetLinqSource(orderDescInvocation, out var orderDescSource))
		{
			result = CreateInvocation(visit(orderDescSource) ?? orderDescSource, nameof(Enumerable.Min));
			return true;
		}
		
		// For arrays, use direct array indexing: arr[^1]
		// For List<T>, use direct indexing: list[^1]
		if (IsInvokedOnArray(model, source) || IsInvokedOnList(model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				visit(source) ?? source,
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
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Last));
			return true;
		}

		result = null;
		return false;
	}
}


