using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for zero divided by non-zero: 0 / x = 0 (integers only)
/// Safe under Strict (integer arithmetic identity).
/// </summary>
public class DivideZeroByNonZeroStrategy : SymmetricStrategy<IntegerBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsNumericZero())
		{
			optimized = null;
			return false;
		}

		optimized = context.Left.Syntax;
		return true;
	}
}
