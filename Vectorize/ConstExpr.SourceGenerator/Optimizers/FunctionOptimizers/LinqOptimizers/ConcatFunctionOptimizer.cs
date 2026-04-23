using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
public class ConcatFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Concat),n => n is 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);
		var concatenatedCollection = context.VisitedParameters[0];

		// Optimization: Merge multiple Concat calls with collection literals
		// MUST run BEFORE visiting (TryExecutePredicates) so that inner Concat calls
		// haven't been transformed to Append yet. Uses the original (unvisited) source.
		// e.g., collection.Concat([1, 2]).Concat([3, 4]) => collection.Concat([1, 2, 3, 4])
		if (TryMergeConcatChain(source, context.OriginalParameters[0], context.Visit, out var mergedResult))
		{
			result = mergedResult;
			return true;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimization: collection.Concat(Enumerable.Empty<T>()) => collection
		if (IsEmptyEnumerable(concatenatedCollection))
		{
			result = source;
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

		// Optimization: collection.Concat([singleElement]) => collection.Append(singleElement)
		// Only apply this if the merge optimization didn't trigger
		if (TryConvertSingleElementConcatToPrepend(context, source, concatenatedCollection, out var prependResult))
		{
			result = prependResult;
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
	/// Tries to convert Concat with a single-element collection to Prepend.
	/// E.g., [42].Concat(collection) => collection.Prepend(42)
	/// </summary>
	private bool TryConvertSingleElementConcatToPrepend(FunctionOptimizerContext context, ExpressionSyntax source, ExpressionSyntax concatenatedCollection, out SyntaxNode? result)
	{
		result = null;

		// Try to get the collection elements
		if (!TryGetCollectionElements(source, out var elements))
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
		result = TryOptimizeByOptimizer<AppendFunctionOptimizer>(context, CreateInvocation(concatenatedCollection, nameof(Enumerable.Prepend), expressionElement.Expression));
		return true;
	}

	/// <summary>
	/// Tries to merge a chain of Concat operations with collection literals.
	/// Walks the entire chain recursively to handle 3+ Concat operations.
	/// E.g., collection.Concat([1]).Concat([2]).Concat([3]) => collection.Concat([1, 2, 3])
	/// </summary>
	private bool TryMergeConcatChain(ExpressionSyntax source, ExpressionSyntax currentCollection, Func<SyntaxNode, ExpressionSyntax?> visit, out SyntaxNode? result)
	{
		result = null;

		// The current collection must be a collection literal
		if (!TryGetCollectionElements(currentCollection, out var currentElements))
		{
			return false;
		}

		// Walk backwards through the Concat chain, collecting all collection literals
		var chainCollections = new List<List<CollectionElementSyntax>> { currentElements };
		var currentSource = source;

		while (IsLinqMethodChain(currentSource, nameof(Enumerable.Concat), out var previousConcatInvocation))
		{
			if (previousConcatInvocation.ArgumentList.Arguments.Count != 1)
			{
				break;
			}

			var previousCollection = previousConcatInvocation.ArgumentList.Arguments[0].Expression;

			if (!TryGetCollectionElements(previousCollection, out var previousElements))
			{
				break;
			}

			chainCollections.Insert(0, previousElements);

			if (!TryGetLinqSource(previousConcatInvocation, out currentSource))
			{
				break;
			}
		}

		// Need at least 2 Concat calls with collection literals to merge
		if (chainCollections.Count < 2)
		{
			return false;
		}

		// Merge all elements into a single collection
		var mergedElements = new List<CollectionElementSyntax>();

		foreach (var elements in chainCollections)
		{
			mergedElements.AddRange(elements);
		}

		var mergedCollection = CollectionExpression(SeparatedList(mergedElements));

		// Create the optimized Concat call with the base source (before the chain)
		result = CreateInvocation(visit(currentSource) ?? currentSource, nameof(Enumerable.Concat), mergedCollection);
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