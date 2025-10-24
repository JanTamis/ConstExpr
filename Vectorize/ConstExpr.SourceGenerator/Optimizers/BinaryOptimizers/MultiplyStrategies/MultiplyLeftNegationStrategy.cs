using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for left negation: (-x) * y => -(x * y) (pure)
/// </summary>
public class MultiplyLeftNegationStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression };
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var leftNeg = (PrefixUnaryExpressionSyntax)context.Left.Syntax;
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression, leftNeg.Operand, context.Right.Syntax)));
	}
}
