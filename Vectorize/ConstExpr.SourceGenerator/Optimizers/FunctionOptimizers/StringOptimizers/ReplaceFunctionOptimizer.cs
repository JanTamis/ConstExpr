using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes Replace calls:
/// - s.Replace("a", "a") → s (replacing with same value is no-op)
/// - s.Replace('a', 'a') → s
/// </summary>
public class ReplaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Replace")
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out _) || method.IsStatic || parameters.Count != 2)
		{
			return false;
		}

		// Check if both parameters are the same literal
		if (parameters[0] is LiteralExpressionSyntax first &&
		    parameters[1] is LiteralExpressionSyntax second)
		{
			var firstValue = first.Token.Value;
			var secondValue = second.Token.Value;

			if (Equals(firstValue, secondValue))
			{
				// s.Replace("a", "a") → s
				result = Instance;
				return true;
			}
		}

		return false;
	}
}

