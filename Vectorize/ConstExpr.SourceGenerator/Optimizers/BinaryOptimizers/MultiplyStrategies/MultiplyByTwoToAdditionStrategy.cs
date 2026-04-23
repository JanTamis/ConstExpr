using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to addition: 2 * x => x + x (pure, non-integer)
/// Requires AssociativeMath for floating-point safety (rearranges operations).
/// </summary>
public class MultiplyByTwoToAdditionStrategy : SymmetricStrategy<NumericBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.AssociativeMath;

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsNumericTwo()
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = ParenthesizedExpression(AddExpression(context.Right.Syntax, context.Right.Syntax));
		return true;
	}
}