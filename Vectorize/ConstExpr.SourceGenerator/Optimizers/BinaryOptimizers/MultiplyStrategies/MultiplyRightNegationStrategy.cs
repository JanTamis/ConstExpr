using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for right negation: x * (-y) => -(x * y) (pure)
/// </summary>
public class MultiplyRightNegationStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression };
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var rightNeg = (PrefixUnaryExpressionSyntax)context.Right.Syntax;
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, context.Left.Syntax, rightNeg.Operand)));
	}
}
