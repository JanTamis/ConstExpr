using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.All method.
/// Optimizes patterns such as:
/// - collection.Where(predicate1).All(predicate2) => collection.All(x => predicate1(x) && predicate2(x))
/// - collection.Select(...).All() => collection.All() (projection doesn't affect all-check)
/// - collection.Distinct().All() => collection.All() (distinctness doesn't affect all-check)
/// - collection.OrderBy(...).All() => collection.All() (ordering doesn't affect all-check)
/// - collection.OrderByDescending(...).All() => collection.All() (ordering doesn't affect all-check)
/// - collection.Order().All() => collection.All() (ordering doesn't affect all-check)
/// - collection.OrderDescending().All() => collection.All() (ordering doesn't affect all-check)
/// - collection.ThenBy(...).All() => collection.All() (secondary ordering doesn't affect all-check)
/// - collection.ThenByDescending(...).All() => collection.All() (secondary ordering doesn't affect all-check)
/// - collection.Reverse().All() => collection.All() (reversing doesn't affect all-check)
/// - collection.AsEnumerable().All() => collection.All() (type cast doesn't affect all-check)
/// - collection.ToList().All() => collection.All() (materialization doesn't affect all-check)
/// - collection.ToArray().All() => collection.All() (materialization doesn't affect all-check)
/// </summary>
public class AllFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.All), 1)
{
	// Operations that don't affect the all-check (only order/form/duplicates/materialization)
	private static readonly HashSet<string> OperationsThatDontAffectAll =
	[
		nameof(Enumerable.Distinct), // Deduplication: may reduce count, but if all satisfy condition, All() is true
		nameof(Enumerable.OrderBy), // Ordering: changes order but not all-check
		nameof(Enumerable.OrderByDescending), // Ordering: changes order but not all-check
		"Order", // Ordering (.NET 6+): changes order but not all-check
		"OrderDescending", // Ordering (.NET 6+): changes order but not all-check
		nameof(Enumerable.ThenBy), // Secondary ordering: changes order but not all-check
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not all-check
		nameof(Enumerable.Reverse), // Reversal: changes order but not all-check
		nameof(Enumerable.AsEnumerable), // Type cast: doesn't change the collection
		nameof(Enumerable.ToList), // Materialization: creates list but doesn't filter
		nameof(Enumerable.ToArray), // Materialization: creates array but doesn't filter
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Get the predicate from All(predicate)
		var allPredicate = GetMethodArguments(invocation)
			.Select(s => s.Expression)
			.FirstOrDefault();

		if (allPredicate == null || !TryGetLambda(allPredicate, out var allLambda))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect all-check
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectAll, out source);

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var wherePredicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			// Continue skipping operations before Where as well
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectAll, out source);

			allLambda = CombinePredicates(visit(allLambda) as LambdaExpressionSyntax ?? allLambda, visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate);
			isNewSource = true;
		}

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && GetMethodArguments(selectInvocation).FirstOrDefault() is { Expression: { } selectpredicateArg }
		    && TryGetLambda(selectpredicateArg, out var selectPredicate)
		    && TryGetLinqSource(selectInvocation, out var selectSource))
		{
			// Continue skipping operations before Where as well
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectAll, out source);

			allLambda = CombineSelectLambdas(visit(allLambda) as LambdaExpressionSyntax ?? allLambda, visit(selectPredicate) as LambdaExpressionSyntax ?? selectPredicate);
			isNewSource = true;
		}

		if (IsInvokedOnArray(model, source))
		{
			result = CreateInvocation(SyntaxFactory.ParseTypeName(nameof(Array)), nameof(Array.TrueForAll), visit(source) ?? source, visit(allLambda) ?? allLambda);
			return true;
		}

		if (IsInvokedOnArray(model, source))
		{
			result = CreateInvocation(source, "TrueForAll", visit(allLambda) ?? allLambda);
			return true;
		}

		// If we skipped any operations, create optimized All() call
		if (isNewSource)
		{
			result = CreateInvocation(source!, nameof(Enumerable.All), visit(allLambda) ?? allLambda);
			return true;
		}

		result = null;
		return false;
	}

	private LambdaExpressionSyntax CombineSelectLambdas(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// Replace the outer lambda's parameter with the inner lambda's body
		var combinedBody = ReplaceIdentifier(outerBody, outerParam, innerBody);

		// Create a new lambda with the inner parameter and the combined body
		return SyntaxFactory.SimpleLambdaExpression(
			SyntaxFactory.Parameter(SyntaxFactory.Identifier(innerParam)),
			combinedBody
		);
	}
}