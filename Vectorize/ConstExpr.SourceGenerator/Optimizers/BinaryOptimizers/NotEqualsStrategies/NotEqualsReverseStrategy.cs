using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   Strategy that normalizes reversed comparisons: literal != x → x != literal.
///   Puts the variable on the left-hand side in canonical form.
/// </summary>
public class NotEqualsReverseStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Right.Syntax is LiteralExpressionSyntax)
		{
			optimized = null;
			return false;
		}

		optimized = NotEqualsExpression(context.Right.Syntax, context.Left.Syntax);
		return true;
	}
}