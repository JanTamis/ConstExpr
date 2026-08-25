using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Strategy for division by power of two: x / (2^n) => x >> n (for unsigned) or (x + ((x >> (bitSize - 1)) & (2^n - 1)))
///   >> n (for signed)
///   Safe under Strict (integer arithmetic identity).
/// </summary>
public class DivideByPowerOfTwoToShiftStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsNumericPowerOfTwo(out var power))
		{
			return false;
		}

		var isPositive = IsPositive(context, context.Left.Syntax, false);

		if (context.Type.IsUnsignedInteger() || isPositive)
		{
			optimized = RightShiftExpression(context.Left.Syntax, CreateLiteral(power));
			return true;
		}

		if (!IsPure(context.Left.Syntax))
		{
			// Not provably side-effect-free - don't duplicate, use regular division
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
		var signExtract = RightShiftExpression(context.Left.Syntax, CreateLiteral(bitSize - 1));

		// (2^n - 1) - bias mask
		var bias = (1 << power) - 1;
		var biasLiteral = CreateLiteral(bias);

		if (bias == 1)
		{
			// x >>> (bitSize - 1) - unsigned shift extracts the sign bit as 0/1 directly (no masking needed)
			var signBit = BinaryExpression(SyntaxKind.UnsignedRightShiftExpression, context.Left.Syntax, CreateLiteral(bitSize - 1));
			var adjusted = AddExpression(context.Left.Syntax, ParenthesizedExpression(signBit));

			optimized = RightShiftExpression(ParenthesizeIfNeeded(adjusted), CreateLiteral(power));
		}
		else
		{
			// (x >> (bitSize - 1)) & (2^n - 1)
			var maskedSign = BitwiseAndExpression(ParenthesizedExpression(signExtract), biasLiteral);

			// x + ((x >> (bitSize - 1)) & (2^n - 1))
			var adjusted = AddExpression(context.Left.Syntax, ParenthesizedExpression(maskedSign));

			// x + ((x >> (bitSize - 1)) & (2^n - 1)) >> n
			optimized = RightShiftExpression(ParenthesizeIfNeeded(adjusted), CreateLiteral(power));
		}

		return true;
	}

	/// <summary>
	///   Wraps <paramref name="expression" /> in parentheses only if it will become the left operand of a
	///   RightShiftExpression and its own precedence is lower than Shift - otherwise the parens are redundant
	///   (e.g. Additive binds tighter than Shift, so `x + y` needs no parens before `>> n`).
	/// </summary>
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
	{
		return expression.GetOperatorPrecedence() < OperatorPrecedence.Shift
			? ParenthesizedExpression(expression)
			: expression;
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