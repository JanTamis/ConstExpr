using System;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes string.IsNullOrEmpty(literal) to true/false.
/// </summary>
public class IsNullOrEmptyFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrEmpty")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(context.Method, out _) || !context.Method.IsStatic || context.VisitedParameters.Count != 1)
		{
			return false;
		}

		if (context.VisitedParameters[0] is not LiteralExpressionSyntax literal)
		{
			return false;
		}

		if (literal.IsKind(SyntaxKind.NullLiteralExpression))
		{
			result = SyntaxHelpers.CreateLiteral(true);
			return true;
		}

		if (literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			var str = literal.Token.ValueText;
			result = SyntaxHelpers.CreateLiteral(string.IsNullOrEmpty(str));
			return true;
		}

		return false;
	}
}

