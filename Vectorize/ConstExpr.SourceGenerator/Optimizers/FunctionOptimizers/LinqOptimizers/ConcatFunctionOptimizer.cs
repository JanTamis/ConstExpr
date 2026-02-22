using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Concat context.Method.
/// Optimizes patterns such as:
/// - collection.Concat(Enumerable.Empty&lt;T&gt;()) => collection (concatenating empty is a no-op)
/// - Enumerable.Empty&lt;T&gt;().Concat(collection) => collection (concatenating to empty)
/// - collection.AsEnumerable().Concat(other) => collection.Concat(other) (skip type cast)
/// - collection.ToList().Concat(other) => collection.Concat(other) (skip materialization)
/// - collection.ToArray().Concat(other) => collection.Concat(other) (skip materialization)
/// - collection.Concat([1, 2]).Concat([3, 4]) => collection.Concat([1, 2, 3, 4]) (merge collection literals)
/// - collection.Concat([singleElement]) => collection.Append(singleElement) (use Append for single element)
/// - Supports merging chains of 3+ Concat operations with collection literals
/// </summary>
public class ConcatFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Concat), 1)
{
	// Operations that don't affect Concat behavior (type casts and materializations)
	private static readonly HashSet<string> OperationsThatDontAffectConcat =
	[
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: preserves order and all elements
		nameof(Enumerable.ToArray), // Materialization: preserves order and all elements
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectConcat, out source);
		var concatenatedCollection = context.VisitedParameters[0];

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimization: collection.Concat(Enumerable.Empty<T>()) => collection
		if (IsEmptyEnumerable(concatenatedCollection))
		{
			result = context.Visit(source);
			return true;
		}

		// Optimization: Enumerable.Empty<T>().Concat(collection) => collection
		if (IsEmptyEnumerable(source))
		{
			result = concatenatedCollection;
			return true;
		}

		// Optimization: collection.Concat([singleElement]) => collection.Append(singleElement)
		// Only apply this if the merge optimization didn't trigger
		if (TryConvertSingleElementConcatToAppend(context, source, concatenatedCollection, out var appendResult))
		{
			result = appendResult;
			return true;
		}
		
		// Optimization: Merge multiple Concat calls with collection literals
		// e.g., collection.Concat([1, 2]).Concat([3, 4]) => collection.Concat([1, 2, 3, 4])
		if (TryMergeConcatChain(source, concatenatedCollection, context.Visit, out var mergedResult))
		{
			result = mergedResult;
			return true;
		}

		// If we skipped any operations (AsEnumerable/ToList/ToArray), create optimized Concat call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source, concatenatedCollection);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Tries to convert Concat with a single-element collection to Append.
	/// E.g., collection.Concat([42]) => collection.Append(42)
	/// </summary>
	private bool TryConvertSingleElementConcatToAppend(FunctionOptimizerContext context, ExpressionSyntax source, ExpressionSyntax concatenatedCollection, out SyntaxNode? result)
	{
		result = null;

		// Try to get the collection elements
		if (!TryGetCollectionElements(concatenatedCollection, out var elements))
		{
			return false;
		}

		// Check if there's exactly one element
		if (elements.Count != 1)
		{
			return false;
		}

		// Extract the single element
		var singleElement = elements[0];

		// The element must be an ExpressionElement (not a spread element like ..other)
		if (singleElement is not ExpressionElementSyntax expressionElement)
		{
			return false;
		}

		// Create Append call: source.Append(element)
		result = TryOptimizeByOptimizer<AppendFunctionOptimizer>(context, CreateInvocation(source, nameof(Enumerable.Append), expressionElement.Expression));
		return true;
	}

	/// <summary>
	/// Tries to merge a chain of Concat operations with collection literals.
	/// E.g., collection.Concat([1, 2]).Concat([3, 4]) => collection.Concat([1, 2, 3, 4])
	/// </summary>
	private bool TryMergeConcatChain(ExpressionSyntax source, ExpressionSyntax currentCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		result = null;

		// Check if source is another Concat call
		if (!IsLinqMethodChain(source, nameof(Enumerable.Concat), out var previousConcatInvocation))
		{
			return false;
		}

		// Get the collection from the previous Concat call
		if (previousConcatInvocation.ArgumentList.Arguments.Count != 1)
		{
			return false;
		}

		var previousCollection = previousConcatInvocation.ArgumentList.Arguments[0].Expression;

		// Check if both collections are collection literals (array initializers or collection expressions)
		if (!TryGetCollectionElements(previousCollection, out var previousElements))
		{
			return false;
		}

		if (!TryGetCollectionElements(currentCollection, out var currentElements))
		{
			return false;
		}

		// Merge the elements
		var mergedElements = new List<CollectionElementSyntax>();
		mergedElements.AddRange(previousElements);
		mergedElements.AddRange(currentElements);

		// Create a new collection expression with merged elements
		var mergedCollection = CollectionExpression(SeparatedList(mergedElements));

		// Get the base source (before the first Concat)
		if (!TryGetLinqSource(previousConcatInvocation, out var baseSource))
		{
			return false;
		}

		// Create the optimized Concat call
		result = CreateInvocation(visit(baseSource) ?? baseSource, nameof(Enumerable.Concat), mergedCollection);
		return true;
	}

	/// <summary>
	/// Tries to extract collection elements from various collection literal forms.
	/// Supports: new[] { ... }, new T[] { ... }, and collection expressions [...]
	/// </summary>
	private bool TryGetCollectionElements(ExpressionSyntax expression, out List<CollectionElementSyntax> elements)
	{
		elements = [ ];

		switch (expression)
		{
			// Collection expression syntax: [1, 2, 3]
			case CollectionExpressionSyntax collectionExpr:
				elements.AddRange(collectionExpr.Elements);
				return true;

			// Implicit array creation: new[] { 1, 2, 3 }
			case ImplicitArrayCreationExpressionSyntax { Initializer: { } initializer }:
				elements.AddRange(initializer.Expressions.Select(ExpressionElement));
				return true;

			// Explicit array creation: new int[] { 1, 2, 3 }
			case ArrayCreationExpressionSyntax { Initializer: { } initializer }:
				elements.AddRange(initializer.Expressions.Select(ExpressionElement));
				return true;

			default:
				return false;
		}
	}
}