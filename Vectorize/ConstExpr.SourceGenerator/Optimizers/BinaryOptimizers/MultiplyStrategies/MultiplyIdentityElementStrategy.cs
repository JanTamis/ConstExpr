using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for identity element: x * 1 = x and 1 * x = x
/// </summary>
public class MultiplyIdentityElementStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericOne())
		{
			optimized = null;
			return false;
		}

		optimized = context.Left.Syntax;
		return true;
	}
}