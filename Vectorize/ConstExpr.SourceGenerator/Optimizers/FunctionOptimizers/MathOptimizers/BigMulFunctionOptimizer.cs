using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BigMulFunctionOptimizer() : BaseMathFunctionOptimizer("BigMul", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var left = context.VisitedParameters[0];
		var right = context.VisitedParameters[1];

		// Math.BigMul(int a, int b) → (long)a * (long)b
		// Math.BigMul(uint a, uint b) → (ulong)a * (ulong)b
		// This inlines the widening multiply, avoiding the Math.BigMul dispatch overhead.
		SyntaxKind targetKeyword;

		switch (paramType.SpecialType)
		{
			case SpecialType.System_Int32:
			{
				targetKeyword = SyntaxKind.LongKeyword;
				break;
			}
			case SpecialType.System_UInt32:
			{
				targetKeyword = SyntaxKind.ULongKeyword;
				break;
			}
			default:
			{
				result = null;
				return false;
			}
		}

		var targetType = PredefinedType(Token(targetKeyword));

		// Cast both operands to the wider type to ensure the widening multiply.
		var castLeft = CastExpression(targetType, IsSimpleSyntax(left) ? left : ParenthesizedExpression(left));
		var castRight = CastExpression(targetType, IsSimpleSyntax(right) ? right : ParenthesizedExpression(right));

		result = MultiplyExpression(castLeft, castRight);
		return true;
	}

	private static bool IsSimpleSyntax(ExpressionSyntax expr)
		=> expr is IdentifierNameSyntax or LiteralExpressionSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax;
}