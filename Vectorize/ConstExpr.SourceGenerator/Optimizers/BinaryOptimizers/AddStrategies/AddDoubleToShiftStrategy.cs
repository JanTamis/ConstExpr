using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for double-to-shift optimization: x + x => x << 1 (integer, pure)
/// </summary>
public class AddDoubleToShiftStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !LeftEqualsRight(context)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
    {
      return false;
    }

    optimized = LeftShiftExpression(context.Left.Syntax, CreateLiteral(1));
		return true;

	}
}