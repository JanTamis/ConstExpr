using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.UnsignedRightShiftStrategies;

/// <summary>
///   Strategy for combining shifts: (x &gt;&gt;&gt; a) &gt;&gt;&gt; b =&gt; x &gt;&gt;&gt; (a + b), or the
///   literal 0 when the combined shift count reaches or exceeds the operand's bit width.
///   &gt;&gt;&gt; is always logical (zero-fill), so past the bit width every bit is shifted out
///   regardless of x's value or signedness — see LeftShiftCombineStrategy for why naively
///   summing the two literal counts is unsound there.
/// </summary>
public class UnsignedRightShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.UnsignedRightShiftExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = null;

		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || leftShiftValue is not int a
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

		if (bitWidth == 0)
		{
			optimized = null;
			return false;
		}

		var combined = Mod(a, bitWidth) + Mod(b, bitWidth);

		if (combined >= bitWidth)
		{
			optimized = CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
			return true;
		}

		if (!TryCreateLiteral(combined, out var combinedLiteral))
		{
			optimized = null;
			return false;
		}

		optimized = BinaryExpression(SyntaxKind.UnsignedRightShiftExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;

		static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
	}
}