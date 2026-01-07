using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: x * (power of two) => x << n (integer)
/// </summary>
public class MultiplyByPowerOfTwoRightStrategy : IntegerBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsNumericPowerOfTwo(out var power))
			return false;
		
		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		return true;
	}
}
