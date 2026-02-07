using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Prepend method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().Prepend(x) => new[] { x } or simplified form
/// - collection.Append(a).Prepend(b) => can be optimized for specific cases
/// </summary>
public class PrependFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Prepend), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Check for empty source optimization is complex, we would need to ensure the source is actually empty
		// For now, we'll skip complex optimizations and just return false

		result = null;
		return false;
	}
}

