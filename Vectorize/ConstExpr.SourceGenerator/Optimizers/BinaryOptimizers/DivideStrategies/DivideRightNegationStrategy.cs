using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for right negation extraction: x / (-y) => -(x / y)
/// </summary>
public class DivideRightNegationStrategy : BaseBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsKind(SyntaxKind.UnaryMinusExpression)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax.Operand))
		{
			optimized = null;
			return false;
		}

		optimized = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(
				BinaryExpression(SyntaxKind.DivideExpression, 
					context.Left.Syntax, 
					context.Right.Syntax.Operand)));
			
		return true;
	}
}
