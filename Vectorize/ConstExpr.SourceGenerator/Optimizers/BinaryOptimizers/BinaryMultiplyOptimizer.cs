using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryMultiplyOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Multiply;

	public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	{
		result = null;

		if (!Type.IsNumericType())
		{
			return false;
		}
		
		Left.TryGetLiteralValue(loader, variables, out var leftValue);
		Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x * 0 = 0
		if (IsPure(Left) && rightValue.IsNumericZero())
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// 0 * x = 0
		if (leftValue.IsNumericZero() && IsPure(Right))
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

		// x * 2 => x << 1 (integer, pure)
		if (Type.IsInteger() && rightValue.IsNumericValue(2))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			return true;
		}

		// 2 * x => x << 1 (integer, pure)
		if (Type.IsInteger() && leftValue.IsNumericValue(2))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			return true;
		}

		// x * (power of two) => x << n (integer)
		if (Type.IsInteger() && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			
			return true;
		}

		// (power of two) * x => x << n (integer)
		if (Type.IsInteger() && leftValue.IsNumericPowerOfTwo(out power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));

			return true;
		}

		// (-x) * (-y) => x * y (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } leftNeg
		    && Right is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } rightNeg)
		{
			result = BinaryExpression(SyntaxKind.MultiplyExpression, leftNeg.Operand, rightNeg.Operand);
			return true;
		}

		// (-x) * y => -(x * y) (pure, can help with further optimizations)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } leftNeg2)
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, leftNeg2.Operand, Right)));
			return true;
		}

		// x * (-y) => -(x * y) (pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } rightNeg2)
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, Left, rightNeg2.Operand)));
			return true;
		}

		// (x * C1) * C2 => x * (C1 * C2) - combine constants
		if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMul
		    && rightValue != null)
		{
			var hasLeftLeft = leftMul.Left.TryGetLiteralValue(loader, variables, out var leftLeftValue);
			var hasLeftRight = leftMul.Right.TryGetLiteralValue(loader, variables, out var leftRightValue);

			if (hasLeftRight && leftRightValue != null)
			{
				// (x * C1) * C2 => x * (C1 * C2)
				var combined = leftRightValue.Multiply(rightValue);
				if (combined != null)
				{
					result = BinaryExpression(SyntaxKind.MultiplyExpression, leftMul.Left,
						SyntaxHelpers.CreateLiteral(combined));
					return true;
				}
			}
			else if (hasLeftLeft && leftLeftValue != null)
			{
				// (C1 * x) * C2 => x * (C1 * C2)
				var combined = leftLeftValue.Multiply(rightValue);
				if (combined != null)
				{
					result = BinaryExpression(SyntaxKind.MultiplyExpression, leftMul.Right,
						SyntaxHelpers.CreateLiteral(combined));
					return true;
				}
			}
		}

		// C1 * (x * C2) => x * (C1 * C2) - combine constants
		if (Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMul
		    && leftValue != null)
		{
			var hasRightLeft = rightMul.Left.TryGetLiteralValue(loader, variables, out var rightLeftValue);
			var hasRightRight = rightMul.Right.TryGetLiteralValue(loader, variables, out var rightRightValue);

			if (hasRightRight && rightRightValue != null)
			{
				// C1 * (x * C2) => x * (C1 * C2)
				var combined = leftValue.Multiply(rightRightValue);
				if (combined != null)
				{
					result = BinaryExpression(SyntaxKind.MultiplyExpression, rightMul.Left,
						SyntaxHelpers.CreateLiteral(combined));
					return true;
				}
			}
			else if (hasRightLeft && rightLeftValue != null)
			{
				// C1 * (C2 * x) => x * (C1 * C2)
				var combined = leftValue.Multiply(rightLeftValue);
				if (combined != null)
				{
					result = BinaryExpression(SyntaxKind.MultiplyExpression, rightMul.Right,
						SyntaxHelpers.CreateLiteral(combined));
					return true;
				}
			}
		}

		// (x * C1) * (y * C2) => (x * y) * (C1 * C2) - combine constants from both sides
		if (Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } leftMul2
		    && Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.MultiplyExpression } rightMul2)
		{
			var hasLeftLeft = leftMul2.Left.TryGetLiteralValue(loader, variables, out var leftLeftValue2);
			var hasLeftRight = leftMul2.Right.TryGetLiteralValue(loader, variables, out var leftRightValue2);
			var hasRightLeft = rightMul2.Left.TryGetLiteralValue(loader, variables, out var rightLeftValue2);
			var hasRightRight = rightMul2.Right.TryGetLiteralValue(loader, variables, out var rightRightValue2);

			ExpressionSyntax? leftNonConstant = null;
			object? leftConstant = null;
			ExpressionSyntax? rightNonConstant = null;
			object? rightConstant = null;

			if (hasLeftRight && leftRightValue2 != null)
			{
				leftNonConstant = leftMul2.Left;
				leftConstant = leftRightValue2;
			}
			else if (hasLeftLeft && leftLeftValue2 != null)
			{
				leftNonConstant = leftMul2.Right;
				leftConstant = leftLeftValue2;
			}

			if (hasRightRight && rightRightValue2 != null)
			{
				rightNonConstant = rightMul2.Left;
				rightConstant = rightRightValue2;
			}
			else if (hasRightLeft && rightLeftValue2 != null)
			{
				rightNonConstant = rightMul2.Right;
				rightConstant = rightLeftValue2;
			}

			if (leftConstant != null && rightConstant != null && leftNonConstant != null && rightNonConstant != null)
			{
				var combined = leftConstant.Multiply(rightConstant);
				if (combined != null)
				{
					result = BinaryExpression(SyntaxKind.MultiplyExpression,
						BinaryExpression(SyntaxKind.MultiplyExpression, leftNonConstant, rightNonConstant),
						SyntaxHelpers.CreateLiteral(combined));
					return true;
				}
			}
		}
		
		return false;
	}
}
