using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes Contains calls:
/// - "hello".Contains("ell") → true
/// - "hello".Contains("world") → false
/// </summary>
public class ContainsFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Contains", false, n => n is 1)
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
			result = CreateLiteral(str.Contains(literal.Token.ValueText));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression)
		    && literal.Token.Value is char c)
		{
			result = CreateLiteral(str.IndexOf(c) >= 0);
			return true;
		}

		return false;
	}
}