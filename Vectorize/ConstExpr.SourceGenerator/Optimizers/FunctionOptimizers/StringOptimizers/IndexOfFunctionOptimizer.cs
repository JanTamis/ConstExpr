using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes IndexOf calls:
/// - "hello".IndexOf("ell") → 1
/// - "hello".IndexOf("world") → -1
/// </summary>
public class IndexOfFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IndexOf", false, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

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
			result = CreateLiteral(str.IndexOf(literal.Token.ValueText, StringComparison.Ordinal));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression)
		    && literal.Token.Value is char c)
		{
			result = CreateLiteral(str.IndexOf(c));
			return true;
		}

		return false;
	}
}