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
		
		var hasLeftValue = Left.TryGetLiteralValue(loader, variables, out var leftValue);
		var hasRightValue = Right.TryGetLiteralValue(loader, variables, out var rightValue);

		// x * 0 = 0
		if (rightValue.IsNumericZero() && (Type.IsInteger() || FloatingPointMode == FloatingPointEvaluationMode.FastMath))
		{
			result = SyntaxHelpers.CreateLiteral(0.ToSpecialType(Type.SpecialType));
			return true;
		}

		// 0 * x = 0
		if (leftValue.IsNumericZero() && (Type.IsInteger() || FloatingPointMode == FloatingPointEvaluationMode.FastMath))
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
		if (Type.IsInteger() && IsPure(Left) && rightValue.IsNumericValue(2))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			return true;
		}

		// 2 * x => x << 1 (integer, pure)
		if (Type.IsInteger() && IsPure(Right) && leftValue.IsNumericValue(2))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
			return true;
		}

		// x * (power of two) => x << n (integer)
		if (Type.IsInteger() && IsPure(Left) && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			
			return true;
		}

		// (power of two) * x => x << n (integer)
		if (Type.IsInteger() && IsPure(Right) && leftValue.IsNumericPowerOfTwo(out power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));

			return true;
		}

		// x * x => x squared (for integer and floating point with specific types)
		if (Left.IsEquivalentTo(Right) && IsPure(Left))
		{
			// Check if type has Pow method or use multiplication
			// This is a simple strength reduction that can help with further optimizations
			// We keep it as is since x * x is already optimal for most architectures
		}

		// (-x) * (-y) => x * y (pure)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } leftNeg
		    && Right is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } rightNeg
		    && IsPure(leftNeg.Operand) && IsPure(rightNeg.Operand))
		{
			result = BinaryExpression(SyntaxKind.MultiplyExpression, leftNeg.Operand, rightNeg.Operand);
			return true;
		}

		// (-x) * y => -(x * y) (pure, can help with further optimizations)
		if (Left is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } leftNeg2
		    && IsPure(leftNeg2.Operand) && IsPure(Right))
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, leftNeg2.Operand, Right)));
			return true;
		}

		// x * (-y) => -(x * y) (pure)
		if (Right is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } rightNeg2
		    && IsPure(Left) && IsPure(rightNeg2.Operand))
		{
			result = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, Left, rightNeg2.Operand)));
			return true;
		}
		
		return false;
	}
}