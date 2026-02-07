using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToHashSet method.
/// Optimizes patterns such as:
/// - collection.ToHashSet().ToHashSet() => collection.ToHashSet() (redundant ToHashSet)
/// - collection.Distinct().ToHashSet() => collection.ToHashSet() (Distinct is implicit in HashSet)
/// - collection.AsEnumerable().ToHashSet() => collection.ToHashSet()
/// - collection.ToList().ToHashSet() => collection.ToHashSet()
/// </summary>
public class ToHashSetFunctionOptimizer() : BaseLinqFunctionOptimizer("ToHashSet", 0)
{
	private static readonly HashSet<string> OperationsThatDontAffectToHashSet =
	[
		"ToHashSet",
		nameof(Enumerable.Distinct),
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Skip all operations that don't affect ToHashSet result
		if (TryGetOptimizedChainExpression(source, OperationsThatDontAffectToHashSet, out source))
		{
			result = CreateSimpleInvocation(source, "ToHashSet");
			return true;
		}

		result = null;
		return false;
	}
}

