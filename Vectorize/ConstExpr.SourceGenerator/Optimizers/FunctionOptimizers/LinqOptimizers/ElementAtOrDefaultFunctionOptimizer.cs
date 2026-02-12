using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ElementAtOrDefault context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (type cast doesn't affect indexing)
/// - collection.ToList().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (materialization doesn't affect indexing)
/// - collection.ToArray().ElementAtOrDefault(index) => collection.ElementAtOrDefault(index) (materialization doesn't affect indexing)
/// - collection.ElementAtOrDefault(0) => collection.FirstOrDefault() (semantically equivalent, more idiomatic)
/// - collection.Skip(n).ElementAtOrDefault(m) => collection.ElementAtOrDefault(n + m) (index adjustment for Skip)
/// Note: We can't optimize to direct array/list indexing because those throw exceptions for out-of-bounds,
/// while ElementAtOrDefault returns default value.
/// Note: OrderBy/OrderByDescending/Reverse DOES affect element positions, so we don't optimize those!
/// Note: Distinct/Where/Select change the collection, so we don't optimize those either!
/// </summary>
public class ElementAtOrDefaultFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ElementAtOrDefault), 1)
{
	// Operations that don't affect element positions or indexing
	// We CAN'T include ordering operations because they change element positions!
	// We CAN'T include filtering/projection operations because they change the collection!
	private static readonly HashSet<string> OperationsThatDontAffectIndexing =
	[
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves order and all elements
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

		var indexParameter = context.VisitedParameters[0];

		// Recursively skip all operations that don't affect indexing
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectIndexing, out source);

		if (IsLinqMethodChain(source, nameof(Enumerable.Skip), out var skipInvocation)
		    && GetMethodArguments(skipInvocation).FirstOrDefault() is { Expression: { } skipCount })
		{
			if (indexParameter is LiteralExpressionSyntax { Token.Value: int indexValue }
			    && context.Visit(skipCount) is LiteralExpressionSyntax { Token.Value: int skipValue })
			{
				// Both index and skip are constant integers, we can compute the new index at compile time
				var newIndex = indexValue + skipValue;

				indexParameter = SyntaxFactory.LiteralExpression(
					SyntaxKind.NumericLiteralExpression,
					SyntaxFactory.Literal(newIndex));
			}
			else
			{
				indexParameter = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, indexParameter, skipCount);
			}

			TryGetLinqSource(skipInvocation, out source);
		}

		if (indexParameter is LiteralExpressionSyntax { Token.Value: 0 })
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.FirstOrDefault));
			return true;
		}

		// If we skipped any operations, create optimized ElementAtOrDefault() call
		if (isNewSource)
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.ElementAtOrDefault), indexParameter);
			return true;
		}

		result = null;
		return false;
	}
}
