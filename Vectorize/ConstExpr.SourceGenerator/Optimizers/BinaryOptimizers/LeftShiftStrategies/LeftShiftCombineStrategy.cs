using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
///   Strategy for combining shifts: ((x &lt;&lt; a) &lt;&lt; b) => x &lt;&lt; (a + b), or the
///   literal 0 when the combined shift count reaches or exceeds the operand's bit width.
///   C# masks each shift count independently (mod bitWidth), so naively summing the two
///   literal counts is wrong whenever that sum reaches the bit width: e.g. for int,
///   (x &lt;&lt; 20) &lt;&lt; 20 is 0 at runtime, but a naive x &lt;&lt; 40 would mask back down to
///   x &lt;&lt; 8. Left-shifting past the bit width always zeroes every bit, regardless of x.
///   Safe under Strict (integer shift arithmetic).
/// </summary>
public class LeftShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.LeftShiftExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

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

		optimized = LeftShiftExpression(context.Left.Syntax.Left, combinedLiteral);
		return true;

		static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
	}
}