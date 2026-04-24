using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for shifting zero: 0 << x => 0 (pure)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class LeftShiftZeroStrategy : IntegerBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsNumericZero())
    {
      return false;
    }

    optimized = CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
