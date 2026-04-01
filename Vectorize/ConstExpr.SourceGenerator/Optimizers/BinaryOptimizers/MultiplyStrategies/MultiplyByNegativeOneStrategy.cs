using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by negative one: x * -1 = -x and -1 * x = -x
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class MultiplyByNegativeOneStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericNegativeOne())
		{
			optimized = null;
			return false;
		}

		optimized = UnaryMinusExpression(context.Left.Syntax);
		return true;
	}
}
