using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;

/// <summary>
/// Strategy that normalizes reversed comparisons: literal > x → x &lt; literal.
/// Puts the variable on the left-hand side in canonical form.
/// </summary>
public class GreaterThanReverseStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = LessThanOrEqualExpression(context.Right.Syntax, context.Left.Syntax);
		return true;
	}
}

