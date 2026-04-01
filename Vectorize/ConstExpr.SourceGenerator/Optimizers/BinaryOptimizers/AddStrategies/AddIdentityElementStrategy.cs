using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for identity element optimization: x + 0 = x and 0 + x = x
/// This optimization is safe under Strict (no fast-math required).
/// </summary>
public class AddIdentityElementStrategy : SymmetricStrategy<NumericBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Left.Syntax.IsNumericZero())
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		optimized = null;
		return false;
	}
}
