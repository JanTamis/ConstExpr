using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

public class MultiplyReverseStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Right.Syntax is LiteralExpressionSyntax)
		{
			optimized = null;
			return false;
		}

		optimized = MultiplyExpression(context.Right.Syntax, context.Left.Syntax);
		return true;
	}
}