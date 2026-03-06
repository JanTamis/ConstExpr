using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToLookup method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.ToList().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.ToArray().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.OrderBy(...).ToLookup(keySelector) => collection.ToLookup(keySelector) (ordering doesn't affect lookup)
/// - collection.OrderByDescending(...).ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.Reverse().ToLookup(keySelector) => collection.ToLookup(keySelector)
/// - collection.Select(x => x).ToLookup(keySelector) => collection.ToLookup(keySelector) (identity Select is a no-op)
/// - collection.ToLookup(keySelector, x => x) => collection.ToLookup(keySelector) (identity element-selector)
/// - Enumerable.Empty&lt;T&gt;().ToLookup(keySelector) => Enumerable.Empty&lt;T&gt;().ToLookup(keySelector) (no further optimization possible for empty)
/// - collection.Select(selector).ToLookup(keySelector) => collection.ToLookup(x => keySelector(selector(x)), selector) (fold Select into ToLookup)
/// - collection.Where(p1).Where(p2).ToLookup(keySelector) => collection.Where(p1 &amp;&amp; p2).ToLookup(keySelector) (merge chained Where predicates)
/// Note: Unlike ToDictionary, Distinct is NOT redundant before ToLookup because ToLookup groups
/// duplicate keys rather than throwing, so removing Distinct could change group sizes.
/// </summary>
public class ToLookupFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToLookup), 1, 2, 3)
{
	// Operations that don't affect the content of the resulting lookup
	// Note: We do NOT include Distinct here because ToLookup groups duplicates —
	// removing Distinct would change the number of elements within each group.
	private static readonly HashSet<string> OperationsThatDontAffectLookup =
	[
		..MaterializingMethods,
		..OrderingOperations,
	];

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectLookup, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize ToLookup(keySelector, x => x) => ToLookup(keySelector) when element-selector is identity
		if (context.VisitedParameters.Count == 2
		    && TryGetLambda(context.VisitedParameters[1], out var elementSelectorLambda)
		    && IsIdentityLambda(elementSelectorLambda))
		{
			result = CreateInvocation(source, nameof(Enumerable.ToLookup), context.VisitedParameters[0]);
			return true;
		}

		// Walk the chain for Select / Where optimizations
		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize Select(selector).ToLookup(keySelector) =>
				//   ToLookup(x => keySelector(selector(x)), selector)
				// When ToLookup has only a keySelector (1 param), fold the Select projection
				// into both the keySelector and a new elementSelector.
				case nameof(Enumerable.Select)
					when context.VisitedParameters.Count == 1
					     && GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
					     && TryGetLambda(selectorArg, out var selectLambda)
					     && TryGetLambda(context.VisitedParameters[0], out var keyLambda):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectLookup, out invocationSource);

					// Compose keySelector ∘ selectLambda
					var composedKey = CombineLambdas(keyLambda, selectLambda);

					// Select(selector).ToLookup(keySelector) => ToLookup(composedKey, selector)
					result = UpdateInvocation(context, invocationSource, composedKey, selectorArg);
					return true;
				}
			}
		}

		// Strip redundant operations (e.g. .ToList().ToLookup() => .ToLookup())
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}