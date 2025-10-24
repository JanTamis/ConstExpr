using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for left negation extraction: (-x) / y => -(x / y)
/// </summary>
public class DivideLeftNegationStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftNeg
		       && IsPure(leftNeg.Operand) 
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not PrefixUnaryExpressionSyntax leftNeg)
		{
			return null;
		}

		return PrefixUnaryExpression(
			SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(
				BinaryExpression(SyntaxKind.DivideExpression, leftNeg.Operand, context.Right.Syntax)));
	}
}
