using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipWhile method.
/// Optimizes patterns such as:
/// - collection.SkipWhile(x => false) => collection (skip nothing)
/// - collection.SkipWhile(x => true) => Enumerable.Empty&lt;T&gt;() (skip everything)
/// </summary>
public class SkipWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SkipWhile), 1)
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

		// Optimize SkipWhile(x => false) => collection (never skip anything)
		if (IsLiteralBooleanLambda(lambda, out var value) && value == false)
		{
			result = visit(source) ?? source;
			return true;
		}

		// Optimize SkipWhile(x => true) => Enumerable.Empty<T>() (skip everything)
		if (IsLiteralBooleanLambda(lambda, out value) && value == true)
		{
			result = CreateEmptyEnumerableCall(method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

