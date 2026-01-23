using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.FirstOrDefault method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).FirstOrDefault() => collection.FirstOrDefault(predicate)
/// - collection.AsEnumerable().FirstOrDefault() => collection.FirstOrDefault() (type cast doesn't affect first)
/// - collection.ToList().FirstOrDefault() => collection.FirstOrDefault() (materialization doesn't affect first)
/// - collection.ToArray().FirstOrDefault() => collection.FirstOrDefault() (materialization doesn't affect first)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect which element is first, so we don't optimize those!
/// Note: Distinct might remove the first element if it's a duplicate, so we don't optimize that either!
/// </summary>
public class FirstOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.FirstOrDefault), 0)
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

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect which element is first
		var currentSource = source;
		while (IsLinqMethodChain(currentSource, OperationsThatDontAffectFirst, out var chainInvocation)
		       && TryGetLinqSource(chainInvocation, out var innerSource))
		{
			currentSource = innerSource;
		}

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var predicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			// Continue skipping operations before Where as well
			while (IsLinqMethodChain(whereSource, OperationsThatDontAffectFirst, out var beforeWhereInvocation)
			       && TryGetLinqSource(beforeWhereInvocation, out var beforeWhereSource))
			{
				whereSource = beforeWhereSource;
			}
			
			result = CreateLinqMethodCall(whereSource, nameof(Enumerable.FirstOrDefault), SyntaxFactory.Argument(predicate));
			return true;
		}

		// If we skipped any operations, create optimized FirstOrDefault() call
		if (currentSource != source)
		{
			result = CreateLinqMethodCall(currentSource, nameof(Enumerable.FirstOrDefault));
			return true;
		}

		result = null;
		return false;
	}
}
