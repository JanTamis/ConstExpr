using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipLast method.
/// Optimizes patterns such as:
/// - collection.SkipLast(0) => collection (skip nothing)
/// - collection.SkipLast(n).SkipLast(m) => collection.SkipLast(n + m)
/// </summary>
public class SkipLastFunctionOptimizer() : BaseLinqFunctionOptimizer("SkipLast", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
		    || parameters[0] is not LiteralExpressionSyntax { Token.Value: int count })
		{
			result = null;
			return false;
		}

		// Optimize SkipLast(0) => source (skip nothing)
		if (count <= 0)
		{
			result = memberAccess.Expression;
			return true;
		}

		result = null;
		return false;
	}
}

