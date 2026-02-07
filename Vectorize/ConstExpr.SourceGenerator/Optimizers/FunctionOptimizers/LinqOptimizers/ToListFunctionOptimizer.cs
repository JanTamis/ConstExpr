using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToList method.
/// Optimizes patterns such as:
/// - collection.ToList().ToList() => collection.ToList() (redundant ToList)
/// - collection.ToArray().ToList() => collection.ToList()
/// - collection.AsEnumerable().ToList() => collection.ToList()
/// </summary>
public class ToListFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToList), 0)
{
	private static readonly HashSet<string> MaterializingMethods =
	[
		nameof(Enumerable.ToArray),
		nameof(Enumerable.ToList),
		nameof(Enumerable.AsEnumerable),
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Skip all materializing/type-cast operations
		if (TryGetOptimizedChainExpression(source, MaterializingMethods, out source))
		{
			result = CreateSimpleInvocation(visit(source) ?? source, nameof(Enumerable.ToList));
			return true;
		}

		result = null;
		return false;
	}
}
