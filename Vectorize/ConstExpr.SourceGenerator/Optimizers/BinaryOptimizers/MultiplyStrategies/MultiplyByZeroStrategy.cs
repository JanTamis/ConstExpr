using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by zero: x * 0 = 0 (pure)
/// </summary>
public class MultiplyByZeroStrategy : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsNumericZero()
		    || !IsPure(context.Left.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = SyntaxHelpers.CreateLiteral(0.ToSpecialType(context.Type.SpecialType));
		return true;
	}
}
