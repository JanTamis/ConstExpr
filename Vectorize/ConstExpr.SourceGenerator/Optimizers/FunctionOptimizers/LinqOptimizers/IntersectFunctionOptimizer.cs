using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Intersect context.Method.
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

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source)
		    || context.VisitedParameters.Count == 0)
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		var intersectCollection = context.VisitedParameters[0];

		// Try simple optimizations first
		if (TryOptimizeEmptySource(context.Visit(source) ?? source, out result)
		    || TryOptimizeEmptyIntersectCollection(context.Method, context.Visit(intersectCollection) ?? intersectCollection, out result)
		    || TryOptimizeSelfIntersect(context, context.Visit(source) ?? source, context.Visit(intersectCollection) ?? intersectCollection, out result)
		    || TryOptimizeChainedIntersect(context, source, intersectCollection, context.Visit, out result))
			return true;

		// Try to optimize by removing redundant operations
		return TryOptimizeRedundantOperations(context, context.Invocation, source, intersectCollection, context.Visit, out result);
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

	private bool TryOptimizeSelfIntersect(FunctionOptimizerContext context, ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(collection) => collection.Distinct()
		// Note: This is a simple syntactic check; semantic equality would be more complex
		if (AreSyntacticallyEquivalent(source, intersectCollection))
		{
			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Distinct)));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeChainedIntersect(FunctionOptimizerContext context, ExpressionSyntax source, ExpressionSyntax intersectCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(other).Intersect(third) => collection.Intersect(other.Intersect(third))
		// This is mathematically equivalent and may enable further optimizations
		if (IsLinqMethodChain(source, nameof(Enumerable.Intersect), out var intersectInvocation)
		    && GetMethodArguments(intersectInvocation).FirstOrDefault() is { Expression: { } firstIntersectArg }
		    && TryGetLinqSource(intersectInvocation, out var intersectSource))
		{
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(firstIntersectArg, nameof(Enumerable.Distinct), intersectCollection));
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(intersectSource, nameof(Enumerable.Distinct), result as ExpressionSyntax));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeRedundantOperations(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, ExpressionSyntax source, ExpressionSyntax intersectCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
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
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Distinct), intersectCollection));
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



