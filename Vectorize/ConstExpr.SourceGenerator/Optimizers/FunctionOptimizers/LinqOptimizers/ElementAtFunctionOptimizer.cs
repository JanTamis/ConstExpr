using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ElementAt method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ElementAt(index) => collection.ElementAt(index) (type cast doesn't affect indexing)
/// - collection.ToList().ElementAt(index) => collection.ElementAt(index) (materialization doesn't affect indexing)
/// - collection.ToArray().ElementAt(index) => collection.ElementAt(index) (materialization doesn't affect indexing)
/// - array.ElementAt(index) => array[index] (direct array access is faster)
/// - list.ElementAt(index) => list[index] (direct list indexing is faster)
/// - collection.ElementAt(0) => collection.First() (semantically equivalent, more idiomatic)
/// - collection.Skip(n).ElementAt(m) => collection.ElementAt(n + m) => collection[n + m] (index adjustment for Skip)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect element positions, so we don't optimize those!
/// Note: Distinct/Where/Select change the collection, so we don't optimize those either!
/// </summary>
public class ElementAtFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ElementAt), 1)
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

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source)
		    || parameters.Count == 0)
		{
			result = null;
			return false;
		}

		var indexParameter = parameters[0];

		// Recursively skip all operations that don't affect indexing
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectIndexing, out source);

		if (IsLinqMethodChain(source, nameof(Enumerable.Skip), out var skipInvocation)
		    && GetMethodArguments(skipInvocation).FirstOrDefault() is { Expression: { } skipCount })
		{
			if (indexParameter is LiteralExpressionSyntax { Token.Value: int indexValue }
			    && skipCount is LiteralExpressionSyntax { Token.Value: int skipValue })
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

		// For arrays, use direct array indexing: arr[index]
		if (IsInvokedOnArray(model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				source,
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(indexParameter))));
			return true;
		}

		// For List<T>, use direct indexing: list[index]
		if (IsInvokedOnList(model, source))
		{
			result = SyntaxFactory.ElementAccessExpression(
				source,
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.Argument(indexParameter))));
			return true;
		}

		if (indexParameter is LiteralExpressionSyntax { Token.Value: 0 })
		{
			result = CreateInvocation(source, nameof(Enumerable.First));
			return true;
		}

		// If we skipped any operations, create optimized ElementAt() call
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.ElementAt), indexParameter);
			return true;
		}

		result = null;
		return false;
	}
}
