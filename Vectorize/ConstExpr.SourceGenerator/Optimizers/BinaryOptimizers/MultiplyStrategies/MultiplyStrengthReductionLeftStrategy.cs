using System;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for strength reduction: C * x => use (x << n) +/- x when applicable
/// e.g. 3 => (x << 1) + x, 5 => (x << 2) + x, 7 => (x << 3) - x, 9 => (x << 3) + x
/// Safe under Strict (integer arithmetic).
/// </summary>
public class MultiplyStrengthReductionLeftStrategy : IntegerBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax, out var leftValue)
		    || !TryGetUInt(leftValue, out var lv)
		    || !IsPure(context.Right.Syntax))
    {
      return false;
    }

    var down = RoundDownToPowerOf2(lv);
		var up = RoundUpToPowerOf2(lv);

		// Pattern: lv = down + 1 => (x << log2(down)) + x
		if (down != 0 && lv == down + 1)
		{
			optimized = ParenthesizedExpression(AddExpression( 
				LeftShiftExpression(context.Right.Syntax,
				CreateLiteral(Log2(down))), context.Right.Syntax));
			
			return true;
		}

		// Pattern: lv = up - 1 => (x << log2(up)) - x
		if (up != 0 && lv == up - 1)
		{
			optimized = ParenthesizedExpression(SubtractExpression( 
				LeftShiftExpression(context.Right.Syntax,
				CreateLiteral(Log2(up))), context.Right.Syntax));
			
			return true;
		}

		return false;
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
    {
      return 0;
    }

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
		
		if (value == null)
    {
      return false;
    }

    try
		{
			result = Convert.ToUInt32(value);
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
