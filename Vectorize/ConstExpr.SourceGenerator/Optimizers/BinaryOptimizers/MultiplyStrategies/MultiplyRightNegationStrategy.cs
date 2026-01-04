using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for right negation: x * (-y) => -(x * y) (pure)
/// </summary>
public class MultiplyRightNegationStrategy : NumericBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Right.Syntax.IsKind(SyntaxKind.UnaryMinusExpression))
			return false;
		
		optimized = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, context.Left.Syntax, context.Right.Syntax.Operand)));
		return true;
	}
}
