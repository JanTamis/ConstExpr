using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
///   Strategy for multiplication by two to shift: 2 * x => x &lt;&lt; 1 (integer)
///   Safe under Strict for both signed and unsigned integers: two's-complement
///   wraparound makes <c>x &lt;&lt; 1</c> identical to <c>2 * x</c> for every integral type
///   (matching the sibling <see cref="MultiplyStrengthReductionStrategy" />, which also
///   gates on <see cref="IntegerBinaryStrategy" />).
/// </summary>
public class MultiplyByTwoToShiftStrategy : SymmetricStrategy<IntegerBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsNumericTwo())
		{
			optimized = null;
			return false;
		}

		optimized = LeftShiftExpression(context.Right.Syntax, CreateLiteral(1));
		return true;
	}
}