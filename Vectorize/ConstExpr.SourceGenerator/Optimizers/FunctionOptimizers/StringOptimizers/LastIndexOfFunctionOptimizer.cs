using System;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes LastIndexOf calls:
/// - "hello".LastIndexOf("l") → 3
/// - "hello".LastIndexOf("world") → -1
/// </summary>
public class LastIndexOfFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "LastIndexOf")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(context.Method, out _) || context.Method.IsStatic || context.VisitedParameters.Count < 1)
		{
			return false;
		}

		if (!TryGetStringInstance(out var str) || str is null)
		{
			return false;
		}

		if (context.VisitedParameters[0] is not LiteralExpressionSyntax literal)
		{
			return false;
		}

		if (literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			var substring = literal.Token.ValueText;
			result = SyntaxHelpers.CreateLiteral(str.LastIndexOf(substring, StringComparison.Ordinal));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char c)
		{
			result = SyntaxHelpers.CreateLiteral(str.LastIndexOf(c));
			return true;
		}

		return false;
	}
}

