using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by negative one: x / -1 = -x
/// </summary>
public class DivideByNegativeOneStrategy : NumericBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.IsNumericNegativeOne())
    {
      return false;
    }

    optimized = UnaryMinusExpression(context.Left.Syntax);
		return true;
	}
}