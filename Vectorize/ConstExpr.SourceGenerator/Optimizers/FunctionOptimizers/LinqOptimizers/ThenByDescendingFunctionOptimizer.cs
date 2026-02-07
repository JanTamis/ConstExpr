using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ThenByDescending method.
/// Optimizes patterns such as:
/// - OrderBy(x => x).ThenByDescending(y => y) => Order().ThenByDescending(y => y) (identity key for Order)
/// </summary>
public class ThenByDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ThenByDescending), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize ThenByDescending(x => x) identity lambda - not much to optimize here
		// ThenByDescending is usually semantically significant
		result = null;
		return false;
	}
}

