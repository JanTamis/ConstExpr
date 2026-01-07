using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for left negation: (-x) * y => -(x * y) (pure)
/// </summary>
public class MultiplyNegationStrategy : SymmetricStrategy<NumericBinaryStrategy, PrefixUnaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.UnaryMinusExpression))
		{
			optimized = null;
			return false;
		}

		optimized = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.MultiplyExpression,
				context.Left.Syntax.Operand,
				context.Right.Syntax)));
		return true;
	}
}