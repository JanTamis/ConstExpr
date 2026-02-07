using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeLast method.
/// Optimizes patterns such as:
/// - collection.TakeLast(0) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeLastFunctionOptimizer() : BaseLinqFunctionOptimizer("TakeLast", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || parameters[0] is not LiteralExpressionSyntax { Token.Value: int count })
		{
			result = null;
			return false;
		}

		// Optimize TakeLast(0) => Enumerable.Empty<T>()
		if (count <= 0)
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

