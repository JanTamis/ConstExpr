using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

		// x * (power of two) => x << n (integer)
		if (Type.IsInteger() && IsPure(Left) && rightValue.IsNumericPowerOfTwo(out var power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Left,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			
			return true;
		}

		// (power of two) * x => x << n (integer)
		if (Type.IsInteger() && IsPure(Right) && rightValue.IsNumericPowerOfTwo(out power))
		{
			result = BinaryExpression(SyntaxKind.LeftShiftExpression, Right,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));

			return true;
		}
		
		return false;
	}
}