using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.DefaultIfEmpty method.
/// Optimizes patterns such as:
/// - collection.DefaultIfEmpty().DefaultIfEmpty() => collection.DefaultIfEmpty() (idempotent)
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
	// Operations that don't affect whether a collection is empty
	// These operations preserve element count (if count > 0, result has count > 0)
	private static readonly HashSet<string> OperationsThatDontAffectEmpty =
	[
		nameof(Enumerable.DefaultIfEmpty), // Idempotent: DefaultIfEmpty().DefaultIfEmpty() == DefaultIfEmpty()
		nameof(Enumerable.Distinct),         // May reduce count, but if collection has elements, result has elements
		nameof(Enumerable.OrderBy),          // Ordering: changes order but not emptiness
		nameof(Enumerable.OrderByDescending),// Ordering: changes order but not emptiness
		"Order",                             // Ordering (.NET 6+): changes order but not emptiness
		"OrderDescending",                   // Ordering (.NET 6+): changes order but not emptiness
		nameof(Enumerable.ThenBy),           // Secondary ordering: changes order but not emptiness
		nameof(Enumerable.ThenByDescending), // Secondary ordering: changes order but not emptiness
		nameof(Enumerable.Reverse),          // Reversal: changes order but not emptiness
		nameof(Enumerable.AsEnumerable),     // Type cast: doesn't change the collection
		nameof(Enumerable.ToList),           // Materialization: preserves all elements
		nameof(Enumerable.ToArray),          // Materialization: preserves all elements
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Get the default value parameter if provided
		var defaultValue = parameters.Count > 0 ? parameters[0] : null;

		// Recursively skip all operations that don't affect emptiness
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectEmpty, out source);

		// Special case: if source is also DefaultIfEmpty, we can skip it (idempotent)
		// DefaultIfEmpty(x).DefaultIfEmpty(y) => DefaultIfEmpty(y) (last value wins)
		if (IsLinqMethodChain(source, nameof(Enumerable.DefaultIfEmpty), out var innerDefaultInvocation)
		    && TryGetLinqSource(innerDefaultInvocation, out var innerSource))
		{
			// Continue skipping operations before the inner DefaultIfEmpty
			TryGetOptimizedChainExpression(innerSource, OperationsThatDontAffectEmpty, out innerSource);
			
			// Create new DefaultIfEmpty with current default value (outer one wins)
			if (defaultValue != null)
			{
				result = CreateInvocation(innerSource, nameof(Enumerable.DefaultIfEmpty), defaultValue);
			}
			else
			{
				result = CreateInvocation(innerSource, nameof(Enumerable.DefaultIfEmpty));
			}
			return true;
		}

		// If we skipped any operations, create optimized DefaultIfEmpty() call
		if (isNewSource)
		{
			if (defaultValue != null)
			{
				result = CreateInvocation(source, nameof(Enumerable.DefaultIfEmpty), defaultValue);
			}
			else
			{
				result = CreateInvocation(source, nameof(Enumerable.DefaultIfEmpty));
			}
			return true;
		}

		result = null;
		return false;
	}
}
