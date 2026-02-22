using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.LongCount context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).LongCount() => collection.LongCount(predicate)
/// - collection.Where(p1).Where(p2).LongCount() => collection.LongCount(p1 && p2) (multiple chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3).LongCount() => collection.LongCount(p1 && p2 && p3)
/// - collection.Select(...).LongCount() => collection.LongCount() (projection doesn't affect count for non-null elements)
/// - collection.OrderBy(...).LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.OrderByDescending(...).LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.Order().LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.OrderDescending().LongCount() => collection.LongCount() (ordering doesn't affect count)
/// - collection.ThenBy(...).LongCount() => collection.LongCount() (secondary ordering doesn't affect count)
/// - collection.ThenByDescending(...).LongCount() => collection.LongCount() (secondary ordering doesn't affect count)
/// - collection.Reverse().LongCount() => collection.LongCount() (reversing doesn't affect count)
/// - collection.AsEnumerable().LongCount() => collection.LongCount() (type cast doesn't affect count)
/// - collection.OrderBy(...).Where(p1).Where(p2).LongCount() => collection.LongCount(p1 && p2) (combining operations)
/// </summary>
public class LongCountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.LongCount), 0, 1)
{
	// Operations that don't affect element count (only order/form but not filtering)
	// Note: We DON'T include Distinct, ToList, ToArray because they might affect count
	// - Distinct: reduces count by removing duplicates
	// - ToList/ToArray: materialization could fail/filter
	private static readonly HashSet<string> OperationsThatDontAffectCount =
	[
		nameof(Enumerable.OrderBy),          // Ordering: changes order but not count
		nameof(Enumerable.OrderByDescending),// Ordering: changes order but not count
		"Order",                             // Ordering (.NET 6+): changes order but not count
		"OrderDescending",                   // Ordering (.NET 6+): changes order but not count
		nameof(Enumerable.ThenBy),           // Secondary ordering: changes order but not count
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not count
		nameof(Enumerable.Reverse),          // Reversal: changes order but not count
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.Select)            // Projection: doesn't change count for non-nullable types
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);

		if (TryExecutePredicates(context, context.Visit(source) ?? source, out result))
		{
			return true;
		}

		// Collect all chained Where predicates
		var wherePredicates = new List<LambdaExpressionSyntax>();
		var currentSource = source;

		// Walk through the chain and collect all Where statements
		while (IsLinqMethodChain(currentSource, nameof(Enumerable.Where), out var whereInvocation)
		       && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		       && TryGetLambda(predicateArg, out var predicate)
		       && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			if (IsLiteralBooleanLambda(predicate, out var literalValue) && literalValue == true)
			{
				switch (literalValue)
				{
					case true:
						TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out currentSource);
						continue;
					case false:
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0L));
						return true;
				}
			}
			
			wherePredicates.Add(predicate);
			
			// Skip operations that don't affect count before the next Where
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out currentSource);
		}

		// If we found any Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// Start with the first predicate and combine with the rest
			var combinedPredicate = wherePredicates.Count == 1 
				? wherePredicates[0] 
				: context.Visit(wherePredicates[^1]) as LambdaExpressionSyntax ?? wherePredicates[^1];
			
			// Combine from right to left (last to first)
			for (var i = wherePredicates.Count - 2; i >= 0; i--)
			{
				var currentPredicate = context.Visit(wherePredicates[i]) as LambdaExpressionSyntax ?? wherePredicates[i];
				combinedPredicate = CombinePredicates(currentPredicate, combinedPredicate);
			}

			// If LongCount() has a predicate parameter, combine it as well
			if (context.VisitedParameters is [ LambdaExpressionSyntax lambda ])
			{
				combinedPredicate = CombinePredicates(context.Visit(lambda) as LambdaExpressionSyntax ?? lambda, combinedPredicate);
			}
			
			combinedPredicate = context.Visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true when IsCollectionType(context, currentSource):
					{
						result = SyntaxFactory.CastExpression(
							SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
							CreateMemberAccess(context.Visit(currentSource) ?? currentSource, "Count"));
						return true;
					}
					case true when IsInvokedOnArray(context, currentSource):
					{
						result = SyntaxFactory.CastExpression(
							SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
							CreateMemberAccess(context.Visit(currentSource) ?? currentSource, "Length"));
						return true;
					}
					case false:
					{
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0L));
						return true;
					}
				}
			}
			
			result = CreateInvocation(context.Visit(currentSource) ?? currentSource, nameof(Enumerable.LongCount), combinedPredicate);
			return true;
		}

		if (context.VisitedParameters.Count == 0)
		{
			source = context.Visit(source) ?? source;
			
			if (TryGetSyntaxes(source, out var values))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal((long)values.Count));
				return true;
			}
			
			if (IsCollectionType(context, currentSource))
			{
				result = SyntaxFactory.CastExpression(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
					CreateMemberAccess(currentSource, "Count"));
				return true;
			}

			if (IsCollectionType(context, source))
			{
				result = SyntaxFactory.CastExpression(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
					CreateMemberAccess(source, "Count"));
				return true;
			}

			if (IsInvokedOnArray(context, currentSource))
			{
				result = SyntaxFactory.CastExpression(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
					CreateMemberAccess(currentSource, "Length"));
				return true;
			}

			if (IsInvokedOnArray(context, source))
			{
				result = SyntaxFactory.CastExpression(
					SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
					CreateMemberAccess(source, "Length"));
				return true;
			}
		}

		source = context.Visit(currentSource) ?? currentSource;

		if (IsEmptyEnumerable(source))
		{
			result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0L));
			return true;
		}

		// If we skipped any operations, create optimized LongCount() call
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.LongCount), context.VisitedParameters);
			return true;
		}

		result = null;
		return false;
	}
}

