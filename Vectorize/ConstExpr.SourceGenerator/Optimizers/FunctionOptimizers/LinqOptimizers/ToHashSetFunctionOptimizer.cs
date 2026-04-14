using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToHashSet context.Method.
/// Optimizes patterns such as:
/// - collection.ToHashSet().ToHashSet() => collection.ToHashSet() (redundant ToHashSet)
/// - collection.Distinct().ToHashSet() => collection.ToHashSet() (Distinct is implicit in HashSet)
/// - collection.AsEnumerable().ToHashSet() => collection.ToHashSet()
/// - collection.ToList().ToHashSet() => collection.ToHashSet()
/// </summary>
public class ToHashSetFunctionOptimizer() : BaseLinqFunctionOptimizer("ToHashSet", 0)
{
	private static readonly HashSet<string> OperationsThatDontAffectExistence =
	[
		..MaterializingMethods, // Materialization doesn't affect existence in a HashSet
		..OrderingOperations, // Ordering doesn't affect existence in a HashSet
		nameof(Enumerable.Distinct), // Distinct is implicit in HashSet, so we can skip it
	];
	
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectExistence, out source);

		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		// Skip all materializing/type-cast operations
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}

