using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ThenBy method.
/// Optimizes patterns such as:
/// - OrderBy(x => x).ThenBy(y => y) => Order().ThenBy(y => y) (identity key for Order)
/// </summary>
public class ThenByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ThenBy), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize ThenBy(x => x) identity lambda - not much to optimize here
		// ThenBy is usually semantically significant
		result = null;
		return false;
	}
}

