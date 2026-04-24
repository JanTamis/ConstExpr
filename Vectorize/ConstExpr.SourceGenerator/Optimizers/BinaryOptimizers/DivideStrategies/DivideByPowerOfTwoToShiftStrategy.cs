using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by power of two: x / (2^n) => x >> n (for unsigned) or (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n (for signed)
/// Safe under Strict (integer arithmetic identity).
/// </summary>
public class DivideByPowerOfTwoToShiftStrategy : IntegerBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

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
			optimized = RightShiftExpression(context.Left.Syntax, CreateLiteral(power));
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
		var signExtract = RightShiftExpression(context.Left.Syntax, CreateLiteral(bitSize - 1));

		// (2^n - 1) - bias mask
		var bias = (1 << power) - 1;
		var biasLiteral = CreateLiteral(bias);

		if (bias == 1)
		{
			var adjusted = AddExpression(context.Left.Syntax, ParenthesizedExpression(signExtract));

			optimized = RightShiftExpression(ParenthesizedExpression(adjusted), CreateLiteral(power));
		}
		else
		{
			// (x >> (bitSize - 1)) & (2^n - 1)
			var maskedSign = BitwiseAndExpression(ParenthesizedExpression(signExtract), biasLiteral);

			// x + ((x >> (bitSize - 1)) & (2^n - 1))
			var adjusted = AddExpression(context.Left.Syntax, ParenthesizedExpression(maskedSign));

			// (x + ((x >> (bitSize - 1)) & (2^n - 1))) >> n
			optimized = RightShiftExpression(ParenthesizedExpression(adjusted), CreateLiteral(power));
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