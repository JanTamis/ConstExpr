using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: x * (power of two) => x &lt;&lt; n (integer)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class MultiplyByPowerOfTwoStrategy : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericPowerOfTwo(out var power))
		{
			optimized = null;
			return false;
		}

		optimized = LeftShiftExpression(context.Left.Syntax, CreateLiteral(power));
		return true;
	}
}