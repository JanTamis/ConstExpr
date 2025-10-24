using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for double negation cancellation: (-x) / (-y) => x / y
/// </summary>
public class DivideDoubleNegationStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftNeg
		       && context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightNeg
		       && IsPure(leftNeg.Operand) 
		       && IsPure(rightNeg.Operand);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not PrefixUnaryExpressionSyntax leftNeg)
		{
			return null;
		}

		if (context.Right.Syntax is not PrefixUnaryExpressionSyntax rightNeg)
		{
			return null;
		}

		return BinaryExpression(SyntaxKind.DivideExpression, leftNeg.Operand, rightNeg.Operand);
	}
}
