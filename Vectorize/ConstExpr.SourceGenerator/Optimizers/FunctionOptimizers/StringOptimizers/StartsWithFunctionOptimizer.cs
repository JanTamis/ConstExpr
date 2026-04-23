using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes StartsWith calls:
/// - "hello".StartsWith("hel") → true
/// - "hello".StartsWith("world") → false
/// </summary>
public class StartsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "StartsWith", false, n => n is 1)
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
			var prefix = literal.Token.ValueText;
			result = CreateLiteral(str.StartsWith(prefix));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) 
		    && literal.Token.Value is char c)
		{
			result = CreateLiteral(str.Length > 0 && str[0] == c);
			return true;
		}

		return false;
	}
}

