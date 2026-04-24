using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
public class IntersectFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Intersect), n => n is 1)
{
	// Operations that don't affect the result of Intersect
	private static readonly HashSet<string> OperationsThatDontAffectIntersect =
	[
		..MaterializingMethods,
		nameof(Enumerable.Distinct) // Intersect already applies Distinct
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out _))
		{
			return true;
		}

		var intersectCollection = context.VisitedParameters[0];
		var hasNewCollection = false;

		if (TryGetSyntaxes(intersectCollection, out var intersectCollectionSyntaxes))
		{
			while (IsLinqMethodChain(source, nameof(Enumerable.Intersect), out var intersectInvocation)
			       && GetMethodArguments(intersectInvocation).FirstOrDefault() is { Expression: { } firstIntersectArg }
			       && TryGetLinqSource(intersectInvocation, out var intersectSource)
			       && TryGetSyntaxes(firstIntersectArg, out var innerIntersectCollectionSyntaxes))
			{
				intersectCollectionSyntaxes = intersectCollectionSyntaxes
					.Intersect(innerIntersectCollectionSyntaxes, SyntaxNodeComparer.Get<ExpressionSyntax>())
					.ToList();

				intersectCollection = CollectionExpression(SeparatedList<CollectionElementSyntax>(intersectCollectionSyntaxes.Select(ExpressionElement)));
				hasNewCollection = true;

				TryGetOptimizedChainExpression(intersectSource, OperationsThatDontAffectIntersect, out source);
			}

			if (intersectCollectionSyntaxes.All(a => a is LiteralExpressionSyntax))
			{
				if (intersectCollectionSyntaxes.Count == 0
				    && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
				{
					result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
					return true;
				}

				// Strip ordering operations when followed by set-based operations (Count, Any, etc.)
				// since ordering doesn't affect set membership
				if (IsFollowedBySetBasedOperation(context.Invocation))
				{
					TryGetOptimizedChainExpression(source, new HashSet<string>(OperationsThatDontAffectIntersect.Union(OrderingOperations)), out source);
				}

				// convert to x.Where(x => x is literal1 or literal2 or ...)
				var orPattern = intersectCollectionSyntaxes
					.Select(PatternSyntax (syntax) => ConstantPattern(syntax))
					.Aggregate((left, right) => BinaryPattern(SyntaxKind.OrPattern, left, right));

				var parameter = Parameter(Identifier("x"));
				var isPatternExpression = IsPatternExpression(IdentifierName("x"), orPattern);
				var lambda = SimpleLambdaExpression(parameter, isPatternExpression);

				var distinctSource = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct))) as ExpressionSyntax
				                     ?? CreateSimpleInvocation(source, nameof(Enumerable.Distinct));

				result = TryOptimizeByOptimizer<WhereFunctionOptimizer>(context, CreateInvocation(distinctSource, nameof(Enumerable.Where), lambda));
				return true;
			}
		}

		source = context.Visit(source) ?? source;

		// Try simple optimizations first
		if ((TryOptimizeEmptySource(source, out result)
		     || TryOptimizeEmptyIntersectCollection(context.Method, intersectCollection, out result)
		     || TryOptimizeSelfIntersect(context, source, intersectCollection, out result)
		     || TryOptimizeChainedIntersect(context, source, intersectCollection, out result))
		    && result is not null)
		{
			return true;
		}

		if (hasNewCollection)
		{
			// optimize to collection.Where(x => intersectCollection.Contains(x)) if we were able to flatten multiple intersect collections into one
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Intersect), intersectCollection));
			return true;
		}

		// Try to optimize by removing redundant operations
		return TryOptimizeRedundantOperations(context, context.Invocation, source, intersectCollection, out result);
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
			result = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct)));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeChainedIntersect(FunctionOptimizerContext context, ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
	{
		// Optimization: collection.Intersect(other).Intersect(third) => collection.Intersect(other.Intersect(third))
		// This is mathematically equivalent and may enable further optimizations
		if (IsLinqMethodChain(source, nameof(Enumerable.Intersect), out var intersectInvocation)
		    && GetMethodArguments(intersectInvocation).FirstOrDefault() is { Expression: { } firstIntersectArg }
		    && TryGetLinqSource(intersectInvocation, out var intersectSource))
		{
			var innerIntersect = CreateInvocation(firstIntersectArg, nameof(Enumerable.Intersect), intersectCollection);
			var optimizedInner = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, innerIntersect) as ExpressionSyntax ?? innerIntersect;
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(intersectSource, nameof(Enumerable.Intersect), optimizedInner));
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeRedundantOperations(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, ExpressionSyntax source, ExpressionSyntax intersectCollection, out SyntaxNode? result)
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
			result = TryOptimizeByOptimizer<IntersectFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Intersect), intersectCollection));
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