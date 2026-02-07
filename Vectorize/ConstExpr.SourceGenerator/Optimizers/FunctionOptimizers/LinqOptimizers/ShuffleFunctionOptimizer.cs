using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Shuffle method (.NET 9+).
/// Optimizes patterns such as:
/// - collection.Shuffle().Shuffle() => collection.Shuffle() (multiple shuffles are redundant)
/// - collection.OrderBy(...).Shuffle() => collection.Shuffle() (ordering before shuffle is pointless)
/// - collection.OrderByDescending(...).Shuffle() => collection.Shuffle() (ordering before shuffle is pointless)
/// - collection.Order().Shuffle() => collection.Shuffle() (ordering before shuffle is pointless)
/// - collection.OrderDescending().Shuffle() => collection.Shuffle() (ordering before shuffle is pointless)
/// - collection.ThenBy(...).Shuffle() => collection.Shuffle() (secondary ordering before shuffle is pointless)
/// - collection.ThenByDescending(...).Shuffle() => collection.Shuffle() (secondary ordering before shuffle is pointless)
/// - collection.Reverse().Shuffle() => collection.Shuffle() (reversing before shuffle is pointless)
/// </summary>
public class ShuffleFunctionOptimizer() : BaseLinqFunctionOptimizer("Shuffle", 0)
{
	// Operations that are pointless before shuffling (since shuffle randomizes order anyway)
	private static readonly HashSet<string> OperationsBeforeShuffleThatArePointless =
	[
		"Shuffle",                           // Multiple shuffles are redundant
		nameof(Enumerable.OrderBy),          // Ordering before shuffle is pointless
		nameof(Enumerable.OrderByDescending),// Ordering before shuffle is pointless
		"Order",                             // Ordering (.NET 6+) before shuffle is pointless
		"OrderDescending",                   // Ordering (.NET 6+) before shuffle is pointless
		nameof(Enumerable.ThenBy),           // Secondary ordering before shuffle is pointless
		nameof(Enumerable.ThenByDescending), // Secondary ordering before shuffle is pointless
		nameof(Enumerable.Reverse),          // Reversing before shuffle is pointless
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip all pointless operations before shuffle
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsBeforeShuffleThatArePointless, out source);

		// If we skipped any operations, create optimized Shuffle() call
		if (isNewSource)
		{
			result = CreateInvocation(source, "Shuffle");
			return true;
		}

		result = null;
		return false;
	}
}

