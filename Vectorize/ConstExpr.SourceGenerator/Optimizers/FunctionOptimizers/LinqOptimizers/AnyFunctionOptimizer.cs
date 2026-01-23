using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Any method.
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
public class AnyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Any), 0)
{
	// Operations that don't affect element existence (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectExistence =
	[
		nameof(Enumerable.Select),           // Projection: transforms elements but doesn't filter
		nameof(Enumerable.Distinct),         // Deduplication: may reduce count, but if any exist, Any() is true
		nameof(Enumerable.OrderBy),          // Ordering: changes order but not existence
		nameof(Enumerable.OrderByDescending),// Ordering: changes order but not existence
		"Order",                             // Ordering (.NET 6+): changes order but not existence
		"OrderDescending",                   // Ordering (.NET 6+): changes order but not existence
		nameof(Enumerable.ThenBy),           // Secondary ordering: changes order but not existence
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not existence
		nameof(Enumerable.Reverse),          // Reversal: changes order but not existence
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray),          // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect existence
		var currentSource = source;
		while (IsLinqMethodChain(currentSource, OperationsThatDontAffectExistence, out var chainInvocation)
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
			while (IsLinqMethodChain(whereSource, OperationsThatDontAffectExistence, out var beforeWhereInvocation)
			       && TryGetLinqSource(beforeWhereInvocation, out var beforeWhereSource))
			{
				whereSource = beforeWhereSource;
			}
			
			result = CreateLinqMethodCall(whereSource, nameof(Enumerable.Any), SyntaxFactory.Argument(predicate));
			return true;
		}

		// If we skipped any operations, create optimized Any() call
		if (currentSource != source)
		{
			result = CreateLinqMethodCall(currentSource, nameof(Enumerable.Any));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeOperationThatDoesntAffectExistence(ExpressionSyntax source, out SyntaxNode? result)
	{
		if (IsLinqMethodChain(source, OperationsThatDontAffectExistence, out var invocation)
		    && TryGetLinqSource(invocation, out var innerSource))
		{
			result = CreateLinqMethodCall(innerSource, nameof(Enumerable.Any));
			return true;
		}

		result = null;
		return false;
	}
}
