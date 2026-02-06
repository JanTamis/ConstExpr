using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Intersect method.
/// Optimizes patterns such as:
/// - collection.Intersect(collection) => collection.Distinct() (intersection with itself is just distinct values)
/// - collection.Intersect(Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;T&gt;() (intersection with empty is empty)
/// - Enumerable.Empty&lt;T&gt;().Intersect(collection) => Enumerable.Empty&lt;T&gt;() (empty intersection anything is empty)
/// - collection.AsEnumerable().Intersect(other) => collection.Intersect(other) (type cast doesn't affect intersection)
/// - collection.ToList().Intersect(other) => collection.Intersect(other) (materialization doesn't affect intersection)
/// - collection.ToArray().Intersect(other) => collection.Intersect(other) (materialization doesn't affect intersection)
/// - collection.Distinct().Intersect(other) => collection.Intersect(other) (Intersect already applies Distinct)
/// - collection.Intersect(other).Intersect(third) => collection.Intersect(other.Intersect(third)) (chained Intersect operations)
/// Note: Intersect already applies Distinct to the result, so Distinct operations are redundant
/// Note: OrderBy/Reverse don't affect set membership, but may affect result order - we can skip them when
///       followed by set-based operations
/// </summary>
public class IntersectFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Intersect), 1)
{
	// Operations that don't affect the result of Intersect
	private static readonly HashSet<string> OperationsThatDontAffectIntersect =
	[
		nameof(Enumerable.Distinct),         // Intersect already applies Distinct
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

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source)
		    || parameters.Count == 0)
		{
			result = null;
			return false;
		}

		var intersectCollection = parameters[0];

		// Try simple optimizations first
		if (TryOptimizeEmptySource(source, out result)
		    || TryOptimizeEmptyIntersectCollection(method, intersectCollection, out result)
		    || TryOptimizeSelfIntersect(source, intersectCollection, out result)
		    || TryOptimizeChainedIntersect(source, intersectCollection, out result))
			return true;

		// Try to optimize by removing redundant operations
		return TryOptimizeRedundantOperations(invocation, source, intersectCollection, out result);
	}

	private bool TryOptimizeEmptySource(ExpressionSyntax source, out SyntaxNode? result)
	{
		// Optimization: Enumerable.Empty<T>().Intersect(collection) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = source;
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeEmptyIntersectCollection(IMethodSymbol method, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(Enumerable.Empty<T>()) => Enumerable.Empty<T>()
		// (intersection with empty is empty)
		if (IsEmptyEnumerable(intersectCollection)
		    && method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeSelfIntersect(ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(collection) => collection.Distinct()
		// Note: This is a simple syntactic check; semantic equality would be more complex
		if (AreSyntacticallyEquivalent(source, intersectCollection))
		{
			result = CreateInvocation(source, nameof(Enumerable.Distinct));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeChainedIntersect(ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(other).Intersect(third) => collection.Intersect(other.Intersect(third))
		// This is mathematically equivalent and may enable further optimizations
		if (IsLinqMethodChain(source, nameof(Enumerable.Intersect), out var intersectInvocation)
		    && GetMethodArguments(intersectInvocation).FirstOrDefault() is { Expression: { } firstIntersectArg }
		    && TryGetLinqSource(intersectInvocation, out var intersectSource))
		{
			var mergedIntersectCollection = CreateInvocation(firstIntersectArg, nameof(Enumerable.Intersect), intersectCollection);
			result = CreateInvocation(intersectSource, nameof(Enumerable.Intersect), mergedIntersectCollection);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeRedundantOperations(InvocationExpressionSyntax invocation, ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Determine which operations can be skipped
		var isFollowedBySetOperation = IsFollowedBySetBasedOperation(invocation);
		var allowedOperations = isFollowedBySetOperation
			? new HashSet<string>(OperationsThatDontAffectIntersect.Union(OrderingOperations))
			: OperationsThatDontAffectIntersect;

		// Recursively skip all allowed operations
		var isNewSource = TryGetOptimizedChainExpression(source, allowedOperations, out source);
		var isNewIntersectCollection = TryGetOptimizedChainExpression(intersectCollection, allowedOperations, out intersectCollection);

		// If we optimized anything, create optimized Intersect call
		if (isNewSource || isNewIntersectCollection)
		{
			result = CreateInvocation(source, nameof(Enumerable.Intersect), intersectCollection);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Checks if the Intersect call is followed by a set-based operation that doesn't care about order.
	/// </summary>
	private bool IsFollowedBySetBasedOperation(InvocationExpressionSyntax invocation)
	{
		var parent = invocation.Parent;

		if (parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parentInvocation } memberAccess
		    && parentInvocation.Expression == memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.Text;
			return SetBasedOperations.Contains(methodName);
		}

		return false;
	}
}



