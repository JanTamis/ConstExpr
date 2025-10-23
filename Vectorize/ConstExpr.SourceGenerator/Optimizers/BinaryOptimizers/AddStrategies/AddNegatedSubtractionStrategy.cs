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
public class AddNegatedSubtractionStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } syntax
		       && IsPure(context.Left.Syntax) && IsPure(syntax.Operand);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		// x + (-y) => x - y (pure)
		if (context.Right.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } rightUnary)
		{
			var rightWithoutMinus = rightUnary.Operand;
			return BinaryExpression(SyntaxKind.SubtractExpression, context.Left.Syntax, rightWithoutMinus);
		}

		// -x + y => y - x (pure)
		if (context.Left.Syntax is PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression } leftUnary)
		{
			var leftWithoutMinus = leftUnary.Operand;
			return BinaryExpression(SyntaxKind.SubtractExpression, context.Right.Syntax, leftWithoutMinus);
		}

		return null;
	}
}