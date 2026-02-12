using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes string.IsNullOrWhiteSpace(literal) to true/false.
/// </summary>
public class IsNullOrWhiteSpaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrWhiteSpace")
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
			result = SyntaxHelpers.CreateLiteral(string.IsNullOrWhiteSpace(str));
			return true;
		}

		return false;
	}
}

