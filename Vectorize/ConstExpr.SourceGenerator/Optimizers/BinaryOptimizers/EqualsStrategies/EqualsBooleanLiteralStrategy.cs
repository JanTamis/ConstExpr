using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for boolean literal comparison: x == true => x, x == false => !x
/// </summary>
public class EqualsBooleanLiteralStrategy : BooleanBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;

		switch (context.Right.Syntax.Token.Value)
		{
			case true:
				optimized = context.Left.Syntax;
				break;
			case false:
				optimized = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(context.Left.Syntax));
				break;
			default:
				return false;
		}

		return true;
	}
}
