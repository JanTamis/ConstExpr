using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
///   Strategy for combining shifts: ((x >> a) >> b) => x >> (a + b), or the boundary shift
///   when the combined shift count reaches or exceeds the operand's bit width. C# masks each
///   shift count independently (mod bitWidth), so naively summing the two literal counts is
///   wrong once that sum reaches the bit width (see LeftShiftCombineStrategy). On unsigned
///   types >> is logical (zero-fill), so the boundary result is always 0; on signed types >>
///   sign-extends, so the boundary result saturates to x >> (bitWidth - 1) — a single shift
///   that evaluates to 0 or -1 at runtime depending on x's sign, without needing to know it
///   here.
/// </summary>
public class RightShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.RightShiftExpression)
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
			optimized = context.Type.IsUnsignedInteger()
				? CreateLiteral(0.ToSpecialType(context.Type.SpecialType))
				: RightShiftExpression(context.Left.Syntax.Left, CreateLiteral((bitWidth - 1).ToSpecialType(context.Type.SpecialType)));
			return true;
		}

		if (!TryCreateLiteral(combined, out var combinedLiteral))
		{
			optimized = null;
			return false;
		}

		optimized = RightShiftExpression(context.Left.Syntax.Left, combinedLiteral);
		return true;

		static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
	}
}