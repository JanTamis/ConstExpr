using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for strength reduction: C * x => use (x << n) +/- x when applicable
/// e.g. 3 => (x << 1) + x, 5 => (x << 2) + x, 7 => (x << 3) - x, 9 => (x << 3) + x
/// </summary>
public class MultiplyStrengthReductionLeftStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.HasValue
		       && context.Left.Value != null
		       && TryGetUInt(context.Left.Value, out _)
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (!TryGetUInt(context.Left.Value, out var lv))
			return null;

		var down = RoundDownToPowerOf2(lv);
		var up = RoundUpToPowerOf2(lv);

		// Pattern: lv = down + 1 => (x << log2(down)) + x
		if (down != 0 && lv == down + 1)
		{
			var shifted = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Log2(down))));
			return ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, shifted, context.Right.Syntax));
		}

		// Pattern: lv = up - 1 => (x << log2(up)) - x
		if (up != 0 && lv == up - 1)
		{
			var shifted = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Log2(up))));
			return ParenthesizedExpression(BinaryExpression(SyntaxKind.SubtractExpression, shifted, context.Right.Syntax));
		}

		return null;
	}

	private static uint RoundUpToPowerOf2(uint value)
	{
		--value;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return value + 1;
	}

	private static uint RoundDownToPowerOf2(uint value)
	{
		if (value == 0)
			return 0;

		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return value - (value >> 1);
	}

	private static bool TryGetUInt(object? value, out uint result)
	{
		result = 0;
		if (value == null) return false;

		try
		{
			result = System.Convert.ToUInt32(value);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static int Log2(uint v)
	{
		var n = 0;
		while (v > 1)
		{
			v >>= 1;
			n++;
		}
		return n;
	}
}
