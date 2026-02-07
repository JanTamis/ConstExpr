using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeWhile method.
/// Optimizes patterns such as:
/// - collection.TakeWhile(x => true) => collection (take everything)
/// - collection.TakeWhile(x => false) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.TakeWhile), 1)
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

		// Optimize TakeWhile(x => true) => collection (take everything)
		if (IsLiteralBooleanLambda(lambda, out var value) && value == true)
		{
			result = source;
			return true;
		}

		// Optimize TakeWhile(x => false) => Enumerable.Empty<T>() (take nothing)
		if (IsLiteralBooleanLambda(lambda, out value) && value == false)
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

