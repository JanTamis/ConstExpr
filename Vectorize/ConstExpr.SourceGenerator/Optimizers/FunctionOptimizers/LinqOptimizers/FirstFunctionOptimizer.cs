using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.First method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).First() => collection.First(predicate)
/// - collection.AsEnumerable().First() => collection.First() (type cast doesn't affect first)
/// - collection.ToList().First() => collection.First() (materialization doesn't affect first)
/// - collection.ToArray().First() => collection.First() (materialization doesn't affect first)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect which element is first, so we don't optimize those!
/// Note: Distinct might remove the first element if it's a duplicate, so we don't optimize that either!
/// </summary>
public class FirstFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.First), 0)
{
	// Operations that don't affect which element is "first"
	// We CAN'T include ordering operations because they change which element comes first!
	// We CAN'T include Distinct because the first element might be a duplicate and get removed!
	private static readonly HashSet<string> OperationsThatDontAffectFirst =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect which element is first
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectFirst, out source);

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var predicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectFirst, out whereSource);
			
			result = CreateInvocation(whereSource, nameof(Enumerable.First), predicate);
			return true;
		}
		
		// now check if we have a Reverse at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Reverse), out var reverseInvocation)
		    && TryGetLinqSource(reverseInvocation, out var reverseSource))
		{
			result = CreateInvocation(reverseSource, nameof(Enumerable.Last));
			return true;
		}
		
		// now check if we have a Order at the end of the optimized chain
		if (IsLinqMethodChain(source, "Order", out var orderInvocation)
		    && TryGetLinqSource(orderInvocation, out var orderSource))
		{
			result = CreateInvocation(orderSource, nameof(Enumerable.Min));
			return true;
		}
		
		// now check if we have a OrderDescending at the end of the optimized chain
		if (IsLinqMethodChain(source, "OrderDescending", out var orderDescInvocation)
		    && TryGetLinqSource(orderDescInvocation, out var orderDescSource))
		{
			result = CreateInvocation(orderDescSource, nameof(Enumerable.Max));
			return true;
		}
		
		// For arrays, use direct array indexing: arr[0]
		// For List<T>, use direct indexing: list[0]
		if (IsInvokedOnArray(model, source)
		    || IsInvokedOnList(model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				source,
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
			result = CreateInvocation(source, nameof(Enumerable.First));
			return true;
		}

		result = null;
		return false;
	}
}


