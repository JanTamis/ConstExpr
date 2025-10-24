using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for right negation extraction: x / (-y) => -(x / y)
/// </summary>
public class DivideRightNegationStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightNeg
		       && IsPure(context.Left.Syntax) 
		       && IsPure(rightNeg.Operand);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Right.Syntax is not PrefixUnaryExpressionSyntax rightNeg)
		{
			return null;
		}

		return PrefixUnaryExpression(
			SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(
				BinaryExpression(SyntaxKind.DivideExpression, context.Left.Syntax, rightNeg.Operand)));
	}
}
