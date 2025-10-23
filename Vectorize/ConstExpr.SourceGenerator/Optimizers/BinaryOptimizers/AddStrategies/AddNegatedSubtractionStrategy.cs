using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for negated subtraction optimization:
/// x + (-y) => x - y (pure)
/// -x + y => y - x (pure)
/// </summary>
public class AddNegatedSubtractionStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return CanOptimizeNegatedRight(context) || CanOptimizeNegatedLeft(context);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// x + (-y) => x - y (pure)
		if (CanOptimizeNegatedRight(context))
		{
			var unary = (PrefixUnaryExpressionSyntax)context.Right.Syntax;
			var rightWithoutMinus = unary.Operand;
			return BinaryExpression(SyntaxKind.SubtractExpression, context.Left.Syntax, rightWithoutMinus);
		}

		// -x + y => y - x (pure)
		if (CanOptimizeNegatedLeft(context))
		{
			var unary = (PrefixUnaryExpressionSyntax)context.Left.Syntax;
			var leftWithoutMinus = unary.Operand;
			return BinaryExpression(SyntaxKind.SubtractExpression, context.Right.Syntax, leftWithoutMinus);
		}

		return null;
	}

	private static bool CanOptimizeNegatedRight(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression }
		    && IsPure(context.Left.Syntax) && IsPure(((PrefixUnaryExpressionSyntax)context.Right.Syntax).Operand);
	}

	private static bool CanOptimizeNegatedLeft(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression }
		    && IsPure(context.Right.Syntax) && IsPure(((PrefixUnaryExpressionSyntax)context.Left.Syntax).Operand);
	}
}
