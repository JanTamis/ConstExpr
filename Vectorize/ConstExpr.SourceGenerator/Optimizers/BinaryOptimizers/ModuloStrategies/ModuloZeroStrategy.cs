using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for zero modulo non-zero: 0 % c = 0 (when c != 0)
/// </summary>
public class ModuloZeroStrategy : SymmetricStrategy<IntegerBinaryStrategy, ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
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
