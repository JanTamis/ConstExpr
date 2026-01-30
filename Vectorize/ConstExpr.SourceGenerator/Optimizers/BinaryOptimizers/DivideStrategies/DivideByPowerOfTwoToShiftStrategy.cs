using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by power of two: x / (2^n) => x >> n (for unsigned) or (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n (for signed)
/// </summary>
public class DivideByPowerOfTwoToShiftStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsNumericPowerOfTwo(out var power))
    {
      return false;
    }

    var isPositive = IsPositive(context, context.Left.Syntax);
		
		if (context.Type.IsUnsignedInteger() || isPositive)
		{
			optimized = BinaryExpression(
				SyntaxKind.RightShiftExpression,
				context.Left.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
			
			return true;
		}

		if (!IsSimpleExpression(context.Left.Syntax))
		{
			// Complex expression - don't duplicate, use regular division
			optimized = null;
			return false;
		}

		var bitSize = GetBitSize(context.Type.SpecialType);

		if (bitSize == 0)
		{
			optimized = null;
			return false;
		}

		// x >> (bitSize - 1) - extracts sign bit (0 for positive, -1 for negative)
		var signExtract = BinaryExpression(
			SyntaxKind.RightShiftExpression,
			context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(bitSize - 1)));

		// (2^n - 1) - bias mask
		var bias = (1 << power) - 1;
		var biasLiteral = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(bias));

		if (bias == 1)
		{
			var adjusted = BinaryExpression(
				SyntaxKind.AddExpression,
				context.Left.Syntax,
				ParenthesizedExpression(signExtract));

			optimized = BinaryExpression(
				SyntaxKind.RightShiftExpression,
				ParenthesizedExpression(adjusted),
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}
		else
		{
			// (x >> (bitSize - 1)) & (2^n - 1)
			var maskedSign = BinaryExpression(
				SyntaxKind.BitwiseAndExpression,
				ParenthesizedExpression(signExtract),
				biasLiteral);

			// x + ((x >> (bitSize - 1)) & (2^n - 1))
			var adjusted = BinaryExpression(
				SyntaxKind.AddExpression,
				context.Left.Syntax,
				ParenthesizedExpression(maskedSign));

			// (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n
			optimized = BinaryExpression(
				SyntaxKind.RightShiftExpression,
				ParenthesizedExpression(adjusted),
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}
		
		return true;
	}

	private static int GetBitSize(SpecialType specialType)
	{
		return specialType switch
		{
			SpecialType.System_SByte => 8,
			SpecialType.System_Int16 => 16,
			SpecialType.System_Int32 => 32,
			SpecialType.System_Int64 => 64,
			_ => 0
		};
	}
}