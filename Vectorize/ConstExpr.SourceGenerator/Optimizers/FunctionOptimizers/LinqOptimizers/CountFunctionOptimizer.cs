using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Count method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Count() => collection.Count(predicate)
/// - collection.Where(p1).Where(p2).Count() => collection.Count(p1 && p2) (multiple chained Where statements)
/// - collection.Where(p1).Where(p2).Where(p3).Count() => collection.Count(p1 && p2 && p3)
/// - collection.Select(...).Count() => collection.Count() (projection doesn't affect count for non-null elements)
/// - collection.OrderBy(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderByDescending(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.Order().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderDescending().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.ThenBy(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.ThenByDescending(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.Reverse().Count() => collection.Count() (reversing doesn't affect count)
/// - collection.AsEnumerable().Count() => collection.Count() (type cast doesn't affect count)
/// - collection.OrderBy(...).Where(p1).Where(p2).Count() => collection.Count(p1 && p2) (combining operations)
/// </summary>
public class CountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Count), 0, 1)
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
		nameof(Enumerable.Select)						 // Projection: doesn't change count for non-nullable types
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);

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
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
						return true;
				}
			}
			
			wherePredicates.Add(predicate);
			
			// Skip operations that don't affect count before the next Where
			isNewSource = TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out currentSource) || isNewSource;
		}

		// If we found any Where predicates, combine them
		if (wherePredicates.Count > 0)
		{
			// Start with the first predicate and combine with the rest
			var combinedPredicate = visit(wherePredicates[^1]) as LambdaExpressionSyntax ?? wherePredicates[^1];
			
			// Combine from right to left (last to first)
			for (var i = wherePredicates.Count - 2; i >= 0; i--)
			{
				var currentPredicate = visit(wherePredicates[i]) as LambdaExpressionSyntax ?? wherePredicates[i];
				combinedPredicate = CombinePredicates(currentPredicate, combinedPredicate);
			}

			// If Count() has a predicate parameter, combine it as well
			if (parameters is [ LambdaExpressionSyntax lambda ])
			{
				combinedPredicate = CombinePredicates(visit(lambda) as LambdaExpressionSyntax ?? lambda, combinedPredicate);
			}
			
			combinedPredicate = visit(combinedPredicate) as LambdaExpressionSyntax ?? combinedPredicate;

			if (IsLiteralBooleanLambda(combinedPredicate, out var literalValue))
			{
				switch (literalValue)
				{
					case true when IsCollectionType(model, currentSource):
						result = CreateMemberAccess(visit(currentSource) ?? currentSource, "Count");
						return true;
					case true when IsInvokedOnArray(model, currentSource):
						result = CreateMemberAccess(visit(currentSource) ?? currentSource, "Length");
						return true;
					case false:
						result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
						return true;
				}
			}
			
			currentSource = visit(currentSource) ?? currentSource;

			if (IsEmptyEnumerable(currentSource))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
				return true;
			}
			
			result = CreateInvocation(currentSource, nameof(Enumerable.Count), combinedPredicate);
			return true;
		}

		if (parameters.Count == 0)
		{
			if (IsEmptyEnumerable(visit(currentSource) ?? currentSource))
			{
				result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
				return true;
			}
			
			if (IsCollectionType(model, currentSource))
			{
				result = CreateMemberAccess(visit(currentSource) ?? currentSource, "Count");
				return true;
			}

			if (IsCollectionType(model, source))
			{
				result = CreateMemberAccess(visit(source) ?? source, "Count");
				return true;
			}

			if (IsInvokedOnArray(model, currentSource))
			{
				result = CreateMemberAccess(visit(currentSource) ?? currentSource, "Length");
				return true;
			}

			if (IsInvokedOnArray(model, source))
			{
				result = CreateMemberAccess(visit(source) ?? source, "Length");
				return true;
			}
		}

		source = visit(currentSource) ?? currentSource;

		if (IsCollectionType(model, source))
		{
			result = CreateMemberAccess(source, "Count");
			return true;
		}

		if (IsInvokedOnArray(model, source))
		{
			result = CreateMemberAccess(source, "Length");
			return true;
		}

		if (IsEmptyEnumerable(source))
		{
			result = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
			return true;
		}

		// If we skipped any operations, create optimized Count() call
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.Count), parameters);
			return true;
		}

		result = null;
		return false;
	}
}

