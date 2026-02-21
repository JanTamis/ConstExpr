using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Except context.Method.
/// Optimizes patterns such as:
/// - collection.Except(Enumerable.Empty&lt;T&gt;()) => collection.Distinct() (removing nothing, but Except applies Distinct)
/// - Enumerable.Empty&lt;T&gt;().Except(collection) => Enumerable.Empty&lt;T&gt;() (empty except anything is empty)
/// - collection.Except(collection) => Enumerable.Empty&lt;T&gt;() (set minus itself is empty)
/// - collection.AsEnumerable().Except(other) => collection.Except(other) (type cast doesn't affect set difference)
/// - collection.ToList().Except(other) => collection.Except(other) (materialization doesn't affect set difference)
/// - collection.ToArray().Except(other) => collection.Except(other) (materialization doesn't affect set difference)
/// - collection.Distinct().Except(other) => collection.Except(other) (Except already applies Distinct)
/// - collection.Except(other).Except(third) => collection.Except(other.Concat(third)) (chained Except operations)
/// Note: Except already applies Distinct to the result, so Distinct operations are redundant
/// Note: OrderBy/Reverse don't affect set membership, but may affect result order - we can skip them when
///       followed by set-based operations
/// </summary>
public class ExceptFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Except), 1)
{
	// Operations that don't affect the result of Except
	private static readonly HashSet<string> OperationsThatDontAffectExcept =
	[
		nameof(Enumerable.Distinct),         // Except already applies Distinct
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

		var exceptCollection = context.VisitedParameters[0];

		// Try simple optimizations first
		if (TryOptimizeEmptySource(context.Visit(source) ?? source, out result)
		    || TryOptimizeEmptyExceptCollection(context.Visit(source) ?? source, context.Visit(exceptCollection) ?? exceptCollection, out result)
		    || TryOptimizeSelfExcept(context.Method, context.Visit(source) ?? source, context.Visit(exceptCollection) ?? exceptCollection, out result)
		    || TryOptimizeChainedExcept(source, exceptCollection, context.Visit, out result))
			return true;

		// Try to optimize by removing redundant operations
		return TryOptimizeRedundantOperations(context.Invocation, source, exceptCollection, context.Visit, out result);
	}

	private bool TryOptimizeEmptySource(ExpressionSyntax source, out SyntaxNode? result)
	{
		// Optimization: Enumerable.Empty<T>().Except(collection) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = source;
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeEmptyExceptCollection(ExpressionSyntax source, ExpressionSyntax exceptCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Except(Enumerable.Empty<T>()) => collection.Distinct()
		// (removing nothing, but Except applies Distinct to the result)
		if (IsEmptyEnumerable(exceptCollection))
		{
			result = CreateInvocation(source, nameof(Enumerable.Distinct));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeSelfExcept(IMethodSymbol method, ExpressionSyntax source, ExpressionSyntax exceptCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Except(collection) => Enumerable.Empty<T>()
		// Note: This is a simple syntactic check; semantic equality would be more complex
		if (AreSyntacticallyEquivalent(source, exceptCollection)
		    && method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeChainedExcept(ExpressionSyntax source, ExpressionSyntax exceptCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		// Optimization: collection.Except(other).Except(third) => collection.Except(other.Concat(third))
		if (IsLinqMethodChain(source, nameof(Enumerable.Except), out var exceptInvocation)
		    && GetMethodArguments(exceptInvocation).FirstOrDefault() is { Expression: { } firstExceptArg }
		    && TryGetLinqSource(exceptInvocation, out var exceptSource))
		{
			var mergedExceptCollection = CreateInvocation(visit(firstExceptArg) ?? firstExceptArg, nameof(Enumerable.Concat), visit(exceptCollection));
			result = CreateInvocation(visit(exceptSource) ?? exceptSource, nameof(Enumerable.Except), mergedExceptCollection);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeRedundantOperations(InvocationExpressionSyntax invocation, ExpressionSyntax source, ExpressionSyntax exceptCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		// Determine which operations can be skipped
		var isFollowedBySetOperation = IsFollowedBySetBasedOperation(invocation);
		var allowedOperations = isFollowedBySetOperation
			? new HashSet<string>(OperationsThatDontAffectExcept.Union(OrderingOperations))
			: OperationsThatDontAffectExcept;

		// Recursively skip all allowed operations
		var isNewSource = TryGetOptimizedChainExpression(source, allowedOperations, out source);
		var isNewExceptCollection = TryGetOptimizedChainExpression(exceptCollection, allowedOperations, out exceptCollection);

		// If we optimized anything, create optimized Except call
		if (isNewSource || isNewExceptCollection)
		{
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Except), visit(exceptCollection) ?? exceptCollection);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Checks if the Except call is followed by a set-based operation that doesn't care about order.
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


