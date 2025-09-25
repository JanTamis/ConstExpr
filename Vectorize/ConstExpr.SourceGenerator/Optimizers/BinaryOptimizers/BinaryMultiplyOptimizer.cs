using System.Net;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryMultiplyOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Multiply;

	public override bool TryOptimize(bool hasLeftValue, object? leftValue, bool hasRightValue, object? rightValue, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}

		// x * 0 = 0
		if (rightValue.IsNumericZero())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// 0 * x = 0
		if (leftValue.IsNumericZero())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// x * 1 = x
		if (rightValue.IsNumericOne())
		{
			result = Left;
			return true;
		}

		// 1 * x = x
		if (leftValue.IsNumericOne())
		{
			result = Right;
			return true;
		}

		// x * -1 = -x
		if (rightValue.IsNumericNegativeOne())
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Left);
			return true;
		}

		// -1 * x = -x
		if (leftValue.IsNumericNegativeOne())
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Right);
			return true;
		}

		// x * (power of two) => x << n (integer)
		if (Type.IsInteger() && IsPure(Operation.LeftOperand) && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			
			return true;
		}

		// (power of two) * x => x << n (integer)
		if (Type.IsInteger() && IsPure(Operation.RightOperand) && rightValue.IsNumericPowerOfTwo(out power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));

			return true;
		}
		
		return false;
	}
}