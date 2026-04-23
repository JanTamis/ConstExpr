using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes string.IsNullOrWhiteSpace(literal) to true/false.
/// </summary>
public class IsNullOrWhiteSpaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrWhiteSpace", true, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (context.VisitedParameters[0] is not LiteralExpressionSyntax literal)
		{
			return false;
		}

		if (literal.IsKind(SyntaxKind.NullLiteralExpression))
		{
			result = CreateLiteral(true);
			return true;
		}

		if (literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			result = CreateLiteral(String.IsNullOrWhiteSpace(literal.Token.ValueText));
			return true;
		}

		return false;
	}
}