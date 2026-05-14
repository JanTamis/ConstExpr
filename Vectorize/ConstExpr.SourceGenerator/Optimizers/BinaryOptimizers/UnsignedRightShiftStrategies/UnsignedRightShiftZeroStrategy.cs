using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.UnsignedRightShiftStrategies;

/// <summary>
///   Strategy for shifting zero: 0 &gt;&gt;&gt; x =&gt; 0 (pure)
/// </summary>
public class UnsignedRightShiftZeroStrategy : IntegerBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsNumericZero()
		    || !IsPure(context.Right.Syntax))
		{
			return false;
		}

		optimized = CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}