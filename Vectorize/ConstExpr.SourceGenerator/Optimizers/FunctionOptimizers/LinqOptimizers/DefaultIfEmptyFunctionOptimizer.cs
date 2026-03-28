using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.DefaultIfEmpty context.Method.
/// Optimizes patterns such as:
/// - collection.Distinct().DefaultIfEmpty() => collection.DefaultIfEmpty() (distinctness doesn't affect empty check)
/// - collection.OrderBy(...).DefaultIfEmpty() => collection.DefaultIfEmpty() (ordering doesn't affect empty check)
/// - collection.OrderByDescending(...).DefaultIfEmpty() => collection.DefaultIfEmpty() (ordering doesn't affect empty check)
/// - collection.Order().DefaultIfEmpty() => collection.DefaultIfEmpty() (ordering doesn't affect empty check)
/// - collection.OrderDescending().DefaultIfEmpty() => collection.DefaultIfEmpty() (ordering doesn't affect empty check)
/// - collection.ThenBy(...).DefaultIfEmpty() => collection.DefaultIfEmpty() (secondary ordering doesn't affect empty check)
/// - collection.ThenByDescending(...).DefaultIfEmpty() => collection.DefaultIfEmpty() (secondary ordering doesn't affect empty check)
/// - collection.Reverse().DefaultIfEmpty() => collection.DefaultIfEmpty() (reversing doesn't affect empty check)
/// - collection.AsEnumerable().DefaultIfEmpty() => collection.DefaultIfEmpty() (type cast doesn't affect empty check)
/// - collection.ToList().DefaultIfEmpty() => collection.DefaultIfEmpty() (materialization doesn't affect empty check)
/// - collection.ToArray().DefaultIfEmpty() => collection.DefaultIfEmpty() (materialization doesn't affect empty check)
/// Note: Select, Where, Skip, Take DO affect which elements are present and whether collection is empty!
/// </summary>
public class DefaultIfEmptyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.DefaultIfEmpty), 0, 1)
{

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// Get the default value parameter if provided
		var defaultValue = context.VisitedParameters.FirstOrDefault();

		// Recursively skip all operations that don't affect emptiness
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Special case: if source is also DefaultIfEmpty, we can skip it (idempotent)
		// DefaultIfEmpty(x).DefaultIfEmpty(y) => DefaultIfEmpty(y) (first value wins)
		while (IsLinqMethodChain(source, nameof(Enumerable.DefaultIfEmpty), out var innerDefaultInvocation)
		       && TryGetLinqSource(innerDefaultInvocation, out var innerSource))
		{
			// Continue skipping operations before the inner DefaultIfEmpty
			TryGetOptimizedChainExpression(innerSource, MaterializingMethods, out source);

			defaultValue = innerDefaultInvocation.ArgumentList.Arguments
				.Select(s => s.Expression)
				.FirstOrDefault(); // Update default value to the last one to the last one
			
			isNewSource = true; // We effectively skipped an operation, so we have a new source to optimize from
		}

		// If we skipped any operations, create optimized DefaultIfEmpty() call
		if (isNewSource)
		{
			result = defaultValue != null
				? UpdateInvocation(context, source, defaultValue)
				: UpdateInvocation(context, source);

			return true;
		}

		result = null;
		return false;
	}
}