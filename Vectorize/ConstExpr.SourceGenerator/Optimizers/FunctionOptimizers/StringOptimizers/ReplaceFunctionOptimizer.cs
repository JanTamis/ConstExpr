using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes Replace calls:
/// - s.Replace("a", "a") → s (replacing with same value is no-op)
/// - s.Replace('a', 'a') → s
/// </summary>
public class ReplaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Replace")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(context.Method, out _) || context.Method.IsStatic || context.VisitedParameters.Count != 2)
		{
			return false;
		}

		// Check if both context.Parameters are the same literal
		if (context.VisitedParameters[0] is LiteralExpressionSyntax first &&
		    context.VisitedParameters[1] is LiteralExpressionSyntax second)
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

