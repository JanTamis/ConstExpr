using ConstExpr.SourceGenerator.Helpers;
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
public class StartsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "StartsWith")
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
			var prefix = literal.Token.ValueText;
			result = SyntaxHelpers.CreateLiteral(str.StartsWith(prefix));
			return true;
		}

		if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char c)
		{
			result = SyntaxHelpers.CreateLiteral(str.Length > 0 && str[0] == c);
			return true;
		}

		return false;
	}
}

