using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Count method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Count() => collection.Count(predicate)
/// - collection.Select(...).Count() => collection.Count() (projection doesn't affect count for non-null elements)
/// - collection.OrderBy(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderByDescending(...).Count() => collection.Count() (ordering doesn't affect count)
/// - collection.Order().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.OrderDescending().Count() => collection.Count() (ordering doesn't affect count)
/// - collection.ThenBy(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.ThenByDescending(...).Count() => collection.Count() (secondary ordering doesn't affect count)
/// - collection.Reverse().Count() => collection.Count() (reversing doesn't affect count)
/// - collection.AsEnumerable().Count() => collection.Count() (type cast doesn't affect count)
/// </summary>
public class CountFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Count), 0)
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

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all operations that don't affect count
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCount, out source);

		// Now check if we have a Where at the end of the optimized chain
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var predicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectCount, out whereSource);
			
			result = CreateInvocation(whereSource, nameof(Enumerable.Count), predicate);
			return true;
		}

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

		// If we skipped any operations, create optimized Count() call
		if (isNewSource)
		{
			result = CreateInvocation(source, nameof(Enumerable.Count));
			return true;
		}

		result = null;
		return false;
	}
}

