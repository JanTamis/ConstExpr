using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Contains method.
/// Optimizes patterns such as:
/// - collection.Where(x => x == value).Any() => collection.Contains(value)
/// - collection.Select(...).Contains(value) => collection.Contains(...) (when projection is simple)
/// - collection.Distinct().Contains(value) => collection.Contains(value) (distinctness doesn't affect containment)
/// - collection.OrderBy(...).Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.OrderByDescending(...).Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.Order().Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.OrderDescending().Contains(value) => collection.Contains(value) (ordering doesn't affect containment)
/// - collection.ThenBy(...).Contains(value) => collection.Contains(value) (secondary ordering doesn't affect containment)
/// - collection.ThenByDescending(...).Contains(value) => collection.Contains(value) (secondary ordering doesn't affect containment)
/// - collection.Reverse().Contains(value) => collection.Contains(value) (reversing doesn't affect containment)
/// - collection.AsEnumerable().Contains(value) => collection.Contains(value) (type cast doesn't affect containment)
/// - collection.ToList().Contains(value) => collection.Contains(value) (materialization doesn't affect containment)
/// - collection.ToArray().Contains(value) => collection.Contains(value) (materialization doesn't affect containment)
/// </summary>
public class ContainsFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Contains), 1, 2)
{
	// Operations that don't affect element containment (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectContainment =
	[
		nameof(Enumerable.Distinct),         // Deduplication: may reduce count, but if element exists, Contains is true
		nameof(Enumerable.OrderBy),          // Ordering: changes order but not containment
		nameof(Enumerable.OrderByDescending),// Ordering: changes order but not containment
		"Order",                             // Ordering (.NET 6+): changes order but not containment
		"OrderDescending",                   // Ordering (.NET 6+): changes order but not containment
		nameof(Enumerable.ThenBy),           // Secondary ordering: changes order but not containment
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not containment
		nameof(Enumerable.Reverse),          // Reversal: changes order but not containment
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray),          // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Get the value to search for from Contains(value) or Contains(value, comparer)
		var searchValue = parameters[0];

		// Recursively skip all operations that don't affect containment
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectContainment, out source);

		// Check if we have a Select at the end of the optimized chain
		// and try to invert it: collection.Select(x => x.Prop).Contains(value) => collection.Any(x => x.Prop == value)
		if (IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && GetMethodArguments(selectInvocation).FirstOrDefault() is { Expression: { } selectorArg }
		    && TryGetLambda(selectorArg, out var selector)
		    && TryGetLinqSource(selectInvocation, out var selectSource))
		{
			// Continue skipping operations before Select as well
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectContainment, out selectSource);
			
			selector = visit(selector) as LambdaExpressionSyntax ?? selector;

			// Try to convert to Any with equality check
			// selector is: x => x.Prop
			// searchValue is: value
			// Result should be: x => x.Prop == value
			if (TryGetLambdaBody(selector, out var selectorBody))
			{
				var lambdaParam = GetLambdaParameter(selector);
				var equalityCheck = SyntaxFactory.BinaryExpression(
					SyntaxKind.EqualsExpression,
					selectorBody,
					searchValue);

				var anyPredicate = SyntaxFactory.SimpleLambdaExpression(
					SyntaxFactory.Parameter(SyntaxFactory.Identifier(lambdaParam)),
					equalityCheck);

				// Use appropriate method based on source type
				if (IsInvokedOnList(model, selectSource))
				{
					result = CreateInvocation(visit(selectSource) ?? selectSource, "Exists", visit(anyPredicate) ?? anyPredicate);
					return true;
				}

				if (IsInvokedOnArray(model, selectSource))
				{
					result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.Exists), visit(selectSource) ?? selectSource, visit(anyPredicate) ?? anyPredicate);
					return true;
				}

				result = CreateInvocation(visit(selectSource) ?? selectSource, nameof(Enumerable.Any), visit(anyPredicate) ?? anyPredicate);
				return true;
			}
		}

		// Check if we have a Where at the end of the optimized chain
		// This handles: collection.Where(predicate).Contains(value) => collection.Any(x => predicate(x) && x == value)
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var wherePredicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			// Continue skipping operations before Where as well
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectContainment, out whereSource);
			
			wherePredicate = visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate;

			// Create a new lambda that combines the where predicate with equality check
			var lambdaParam = GetLambdaParameter(wherePredicate);
			var whereBody = GetLambdaBody(wherePredicate);
			var equalityCheck = SyntaxFactory.BinaryExpression(
				SyntaxKind.EqualsExpression,
				SyntaxFactory.IdentifierName(lambdaParam),
				searchValue);

			var combinedBody = SyntaxFactory.BinaryExpression(
				SyntaxKind.LogicalAndExpression,
				SyntaxFactory.ParenthesizedExpression(whereBody),
				SyntaxFactory.ParenthesizedExpression(equalityCheck));

			var anyPredicate = SyntaxFactory.SimpleLambdaExpression(
				SyntaxFactory.Parameter(SyntaxFactory.Identifier(lambdaParam)),
				combinedBody);

			// Use appropriate method based on source type
			if (IsInvokedOnList(model, whereSource))
			{
				result = CreateInvocation(visit(whereSource) ?? whereSource, "Exists", visit(anyPredicate) ?? anyPredicate);
				return true;
			}

			if (IsInvokedOnArray(model, whereSource))
			{
				result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.Exists), visit(whereSource) ?? whereSource, visit(anyPredicate) ?? anyPredicate);
				return true;
			}

			result = CreateInvocation(visit(whereSource) ?? whereSource, nameof(Enumerable.Any), visit(anyPredicate) ?? anyPredicate);
			return true;
		}

		// For List<T>, use the native Contains method
		if (IsInvokedOnList(model, source))
		{
			result = CreateInvocation(visit(source) ?? source, "Contains", visit(searchValue) ?? searchValue);
			return true;
		}

		// For arrays, use Array.IndexOf
		if (IsInvokedOnArray(model, source))
		{
			var indexOfCall = CreateInvocation(
				SyntaxFactory.ParseTypeName(nameof(Array)),
				nameof(Array.IndexOf),
				visit(source) ?? source,
				visit(searchValue) ?? searchValue);

			result = SyntaxFactory.BinaryExpression(
				SyntaxKind.GreaterThanOrEqualExpression,
				indexOfCall,
				SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
			return true;
		}

		// If we skipped any operations, create optimized Contains() call
		if (isNewSource)
		{
			// Keep parameters (including optional comparer)
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Contains), parameters);
			return true;
		}

		result = null;
		return false;
	}
}
