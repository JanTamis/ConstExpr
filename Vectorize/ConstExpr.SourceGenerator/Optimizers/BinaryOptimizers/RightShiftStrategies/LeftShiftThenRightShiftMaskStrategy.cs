using System.Numerics;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
///   Strategy for clearing the high bits of a value that was already left-shifted and shifted
///   back by the same count: (x &lt;&lt; c) &gt;&gt; c => x &amp; ((1 &lt;&lt; (bitWidth - c)) - 1).
///   Unsigned only: '&gt;&gt;' on an unsigned type zero-fills, so the top c bits the left shift
///   discarded stay zero when shifted back. On signed types '&gt;&gt;' sign-extends instead,
///   which does not reduce to a plain mask (see LeftShiftThenUnsignedRightShiftMaskStrategy for
///   the always-safe '&gt;&gt;&gt;' mirror). Two shifts collapse to one AND. Safe under Strict. Bit
///   widths other than 32/64 are excluded: on byte/short, C# promotes both shifts to int
///   before applying them, so this type's own bit width would be wrong (see
///   RightShiftCombineStrategy).
/// </summary>
public class LeftShiftThenRightShiftMaskStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.LeftShiftExpression)
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

		// Unsigned-only: restricting the bit-width switch to the unsigned special types alone
		// makes signed operands fall through to bitWidth == 0 and decline below.
		var bitWidth = context.Type.SpecialType switch
		{
			SpecialType.System_UInt32 => 32,
			SpecialType.System_UInt64 => 64,
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