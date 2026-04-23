using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Shuffle context.Method (.NET 9+).
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
public class ShuffleFunctionOptimizer() : BaseLinqFunctionOptimizer("Shuffle", n => n is 0)
{
	private static readonly HashSet<string> OperationsThatDontAffectShuffle =
	[
		..MaterializingMethods,
		..OrderingOperations
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// If we skipped any operations, create optimized Shuffle() call
		if (TryGetOptimizedChainExpression(context.Visit(source) ?? source, OperationsThatDontAffectShuffle, out var newSource)
		    || !AreSyntacticallyEquivalent(newSource, source))
		{
			result = UpdateInvocation(context, newSource);
			return true;
		}

		result = null;
		return false;
	}
}