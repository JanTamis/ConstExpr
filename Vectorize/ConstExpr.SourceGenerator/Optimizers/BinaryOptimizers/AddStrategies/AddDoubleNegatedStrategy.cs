using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for double negated addition: -x + (-y) => -(x + y) (pure)
/// </summary>
public class AddDoubleNegatedStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftUnary
		       && context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightUnary
		       && IsPure(leftUnary.Operand) && IsPure(rightUnary.Operand);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var leftUnary = (PrefixUnaryExpressionSyntax) context.Left.Syntax;
		var rightUnary = (PrefixUnaryExpressionSyntax) context.Right.Syntax;

		var leftWithoutMinus = leftUnary.Operand;
		var rightWithoutMinus = rightUnary.Operand;

		var addition = ParenthesizedExpression(
			BinaryExpression(SyntaxKind.AddExpression, leftWithoutMinus, rightWithoutMinus));

		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, addition);
	}
}