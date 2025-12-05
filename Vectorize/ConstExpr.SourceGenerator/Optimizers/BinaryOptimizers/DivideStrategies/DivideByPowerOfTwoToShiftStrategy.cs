using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by power of two: x / (2^n) => x >> n (for unsigned) or (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n (for signed)
/// </summary>
public class DivideByPowerOfTwoToShiftStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Type.IsInteger()
					 && context.Right is { HasValue: true, Value: { } value }
					 && value.IsNumericPowerOfTwo(out _);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (!context.Right.Value.IsNumericPowerOfTwo(out var power))
		{
			return null;
		}

		// For unsigned integers: x >> n
		if (context.Type.IsUnsignedInteger())
		{
			return BinaryExpression(
				SyntaxKind.RightShiftExpression,
				context.Left.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}

		// For signed integers: (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n
		// This correctly handles negative numbers by adding a bias before shifting
		// However, this optimization duplicates the left expression, so we should
		// only apply it if the left expression is simple (e.g., a variable or literal).
		// Complex expressions (like multiplication) should use regular division to avoid duplication.
		
		// Check if left expression is simple enough to duplicate
		if (!IsSimpleExpression(context.Left.Syntax))
		{
			// Complex expression - don't duplicate, use regular division
			return null;
		}

		var bitSize = GetBitSize(context.Type.SpecialType);

		if (bitSize == 0)
		{
			return null;
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

			return BinaryExpression(
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
			return BinaryExpression(
				SyntaxKind.RightShiftExpression,
				ParenthesizedExpression(adjusted),
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}
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
