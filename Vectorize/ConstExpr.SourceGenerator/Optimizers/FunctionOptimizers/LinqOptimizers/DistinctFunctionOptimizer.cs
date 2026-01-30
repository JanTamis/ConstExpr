using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Distinct method.
/// Optimizes patterns such as:
/// - collection.Distinct().Distinct() => collection.Distinct() (redundant Distinct calls)
/// - collection.Select(x => x).Distinct() => collection.Distinct() (identity Select before Distinct)
/// - collection.AsEnumerable().Distinct() => collection.Distinct() (type cast doesn't affect distinctness)
/// - collection.ToList().Distinct() => collection.Distinct() (materialization doesn't affect distinctness)
/// - collection.ToArray().Distinct() => collection.Distinct() (materialization doesn't affect distinctness)
/// - collection.OrderBy(...).Distinct().Count() => collection.Distinct().Count() (when followed by set-based operations)
/// Note: OrderBy/Reverse DOES affect the ORDER of distinct results, so we only optimize when followed by
///       operations that don't care about order (Count, Any, Contains, etc.)
/// </summary>
public class DistinctFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Distinct), 0)
{
	// Operations that don't affect the result of Distinct (both values AND order)
	// We CANNOT include ordering operations because they change the ORDER of distinct results!
	private static readonly HashSet<string> OperationsThatDontAffectDistinctness =
	[
		nameof(Enumerable.Distinct),         // Redundant Distinct calls
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and values
		nameof(Enumerable.ToArray),          // Materialization: preserves order and values
	];

	// Operations that change order but can be skipped if followed by set-based operations
	private static readonly HashSet<string> OrderingOperations =
	[
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
	];

	// Operations that only care about the SET of values, not the ORDER
	private static readonly HashSet<string> SetBasedOperations =
	[
		nameof(Enumerable.Count),
		nameof(Enumerable.Any),
		nameof(Enumerable.Contains),
		nameof(Enumerable.LongCount),
		nameof(Enumerable.First),
		nameof(Enumerable.FirstOrDefault),
	];

	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Check if Distinct is followed by a set-based operation
		var parent = invocation.Parent;
		var isFollowedBySetOperation = false;
		
		if (parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parentInvocation } memberAccess
		    && parentInvocation.Expression == memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.Text;
			isFollowedBySetOperation = SetBasedOperations.Contains(methodName);
		}

		// Determine which operations can be skipped
		var allowedOperations = isFollowedBySetOperation
			? new HashSet<string>(OperationsThatDontAffectDistinctness.Union(OrderingOperations))
			: OperationsThatDontAffectDistinctness;

		// Recursively skip all allowed operations
		var currentSource = source;
		
		while (IsLinqMethodChain(currentSource, allowedOperations, out var chainInvocation)
		       && TryGetLinqSource(chainInvocation, out var innerSource))
		{
			currentSource = innerSource;
		}

		// Check for identity Select
		if (IsLinqMethodChain(currentSource, nameof(Enumerable.Select), out var selectInvocation)
		    && GetMethodArguments(selectInvocation).FirstOrDefault() is { Expression: { } lambdaArg }
		    && TryGetLambda(lambdaArg, out var lambda)
		    && IsIdentityLambda(lambda)
		    && TryGetLinqSource(selectInvocation, out var selectSource))
		{
			// Continue skipping operations before identity Select as well
			while (IsLinqMethodChain(selectSource, allowedOperations, out var beforeSelectInvocation)
			       && TryGetLinqSource(beforeSelectInvocation, out var beforeSelectSource))
			{
				selectSource = beforeSelectSource;
			}
			
			result = CreateInvocation(selectSource, nameof(Enumerable.Distinct));
			return true;
		}

		// If we skipped any operations, create optimized Distinct() call
		if (currentSource != source)
		{
			result = CreateInvocation(currentSource, nameof(Enumerable.Distinct));
			return true;
		}

		result = null;
		return false;
	}
}
