using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
		nameof(Enumerable.Distinct), // Except already applies Distinct
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: preserves order and values
		nameof(Enumerable.ToArray), // Materialization: preserves order and values
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
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		var exceptCollection = context.VisitedParameters[0];
		var isNewCollection = false;

		if (TryOptimizeEmptySource(source, out result))
		{
			return true;
		}

		if (TryGetSyntaxes(exceptCollection, out var syntaxes))
		{
			// Collect syntaxes from chained Except calls BEFORE visiting source,
			// so inner Except calls are not prematurely optimized into Where calls.
			// Inner syntaxes are prepended so the final order mirrors the source:
			// x.Except([1]).Except([2]) => x is not (1 or 2) (not (2 or 1)).
			while (IsLinqMethodChain(source, nameof(Enumerable.Except), out var exceptInvocation)
			       && GetMethodArguments(exceptInvocation).FirstOrDefault() is { Expression: { } firstExceptArg }
			       && TryGetLinqSource(exceptInvocation, out var exceptSource)
			       && TryGetSyntaxes(context.Visit(firstExceptArg) ?? firstExceptArg, out var firstExceptSyntaxes))
			{
				// Prepend inner syntaxes so the leftmost Except values appear first.
				var combined = new List<ExpressionSyntax>(firstExceptSyntaxes);
				combined.AddRange(syntaxes);
				syntaxes = combined;

				var isFollowedBySetOperation = IsFollowedBySetBasedOperation(exceptInvocation);
				var allowedOperations = isFollowedBySetOperation
					? new HashSet<string>(OperationsThatDontAffectExcept.Union(OrderingOperations))
					: OperationsThatDontAffectExcept;

				TryGetOptimizedChainExpression(exceptSource, allowedOperations, out source);
			}

			// Now visit the source after all chained Excepts have been collected.
			source = context.Visit(source) ?? source;

			if (syntaxes.All(a => a is LiteralExpressionSyntax))
			{
				if (syntaxes.Count == 0
				    && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
				{
					result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
					return true;
				}

				// convert to x.Where(x => x is not (literal1 or literal2 or ...))
				// For a single literal, omit the parentheses so the rewriter can simplify x is not 1 to x != 1.
				var constantPatterns = syntaxes
					.Select(PatternSyntax (syntax) => SyntaxFactory.ConstantPattern(syntax));

				PatternSyntax notPattern;

				if (syntaxes.Count == 1)
				{
					// x is not literal  →  rewriter will simplify to x != literal
					notPattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword), constantPatterns.First());
				}
				else
				{
					var orPattern = constantPatterns
						.Aggregate((left, right) => SyntaxFactory.BinaryPattern(SyntaxKind.OrPattern, left, right));
					notPattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword), SyntaxFactory.ParenthesizedPattern(orPattern));
				}

				var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("x"));
				var isPatternExpression = SyntaxFactory.IsPatternExpression(SyntaxFactory.IdentifierName("x"), notPattern);
				
				LambdaExpressionSyntax lambda = SyntaxFactory.SimpleLambdaExpression(parameter, isPatternExpression);

				var isFollowedBySetOperation = IsFollowedBySetBasedOperation(context.Invocation);
				var allowedOperations = isFollowedBySetOperation
					? new HashSet<string>(OperationsThatDontAffectExcept.Union(OrderingOperations))
					: OperationsThatDontAffectExcept;

				// Recursively skip all allowed operations
				TryGetOptimizedChainExpression(source, allowedOperations, out source);

				while (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
				       && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } whereArg }
				       && TryGetLinqSource(whereInvocation, out var whereSource)				       
				       && TryGetLambda(whereArg, out var whereLambda))
				{
					lambda = CombinePredicates(lambda, whereLambda);
					
					isFollowedBySetOperation = IsFollowedBySetBasedOperation(whereInvocation);
					allowedOperations = isFollowedBySetOperation
						? new HashSet<string>(OperationsThatDontAffectExcept.Union(OrderingOperations))
						: OperationsThatDontAffectExcept;

					// Recursively skip all allowed operations
					TryGetOptimizedChainExpression(whereSource, allowedOperations, out source);
				}

				var distinctSource = TryOptimizeByOptimizer<DistinctFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.Distinct))) as ExpressionSyntax
				                     ?? CreateSimpleInvocation(source, nameof(Enumerable.Distinct));

				result = TryOptimizeByOptimizer<WhereFunctionOptimizer>(context, CreateInvocation(distinctSource, nameof(Enumerable.Where), context.Visit(lambda) ?? lambda));
				return true;
			}

			exceptCollection = CreateCollection(syntaxes.Distinct(SyntaxNodeComparer<ExpressionSyntax>.Instance));
			isNewCollection = true;
		}

		// Visit the source now that we are in the non-literal fallback path.
		source = context.Visit(source) ?? source;

		// Try simple optimizations first
		if (TryOptimizeEmptySource(source, out result)
		    || TryOptimizeEmptyExceptCollection(source, context.Visit(exceptCollection) ?? exceptCollection, out result)
		    || TryOptimizeSelfExcept(context.Method, source, context.Visit(exceptCollection) ?? exceptCollection, out result)
		    || TryOptimizeChainedExcept(source, exceptCollection, context.Visit, out result))
		{
			return true;
		}

		// Try to optimize by removing redundant operations
		return TryOptimizeRedundantOperations(context, context.Invocation, source, exceptCollection, isNewCollection, out result);
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
			result = CreateSimpleInvocation(source, nameof(Enumerable.Distinct));
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
			var mergedExceptCollection = CreateInvocation(visit(firstExceptArg) ?? firstExceptArg, nameof(Enumerable.Concat), visit(exceptCollection) ?? exceptCollection);
			result = CreateInvocation(visit(exceptSource) ?? exceptSource, nameof(Enumerable.Except), mergedExceptCollection);
			return true;
		}

		result = null;
		return false;
	}

	private bool TryOptimizeRedundantOperations(FunctionOptimizerContext context, InvocationExpressionSyntax invocation, ExpressionSyntax source, ExpressionSyntax exceptCollection, bool isNewCollection, out SyntaxNode? result)
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
		if (isNewSource || isNewExceptCollection || isNewCollection)
		{
			result = CreateInvocation(source, nameof(Enumerable.Except), exceptCollection);
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