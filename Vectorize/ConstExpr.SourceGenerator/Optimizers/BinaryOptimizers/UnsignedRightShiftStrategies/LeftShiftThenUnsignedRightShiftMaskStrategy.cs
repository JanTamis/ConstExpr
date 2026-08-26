using System.Numerics;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.UnsignedRightShiftStrategies;

/// <summary>
///   Strategy for clearing the high bits of a value that was already left-shifted and shifted
///   back with '&gt;&gt;&gt;' by the same count: (x &lt;&lt; c) &gt;&gt;&gt; c => x &amp; ((1 &lt;&lt; (bitWidth - c)) -
///   1).
///   '&gt;&gt;&gt;' is always a logical (zero-fill) shift regardless of signedness, so unlike the
///   plain '&gt;&gt;' mirror in RightShiftStrategies this holds for both signed and unsigned types.
///   Two shifts collapse to one AND. Safe under Strict. Bit widths other than 32/64 are
///   excluded: on byte/short, C# promotes both shifts to int before applying them, so this
///   type's own bit width would be wrong (see UnsignedRightShiftCombineStrategy).
/// </summary>
public class LeftShiftThenUnsignedRightShiftMaskStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.LeftShiftExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var innerShiftValue)
		    || innerShiftValue is not int a
		    || context.Right.Syntax.Token.Value is not int b)
		{
			optimized = null;
			return false;
		}

		var bitWidth = context.Type.SpecialType switch
		{
			SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
			SpecialType.System_Int64 or SpecialType.System_UInt64 => 64,
			_ => 0
		};

		if (bitWidth == 0 || Mod(a, bitWidth) != Mod(b, bitWidth))
		{
			optimized = null;
			return false;
		}

		var c = Mod(a, bitWidth);

		if (c == 0)
		{
			optimized = context.Left.Syntax.Left;
			return true;
		}

		var mask = (BigInteger.One << bitWidth - c) - 1;

		if (!TryFromBigInteger(mask, context.Type.SpecialType, out var maskValue)
		    || !TryCreateLiteral(maskValue, out var maskLiteral))
		{
			optimized = null;
			return false;
		}

		optimized = BitwiseAndExpression(context.Left.Syntax.Left, maskLiteral);
		return true;

		static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
	}
}