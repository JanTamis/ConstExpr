using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for double negation: (-x) * (-y) => x * y (pure)
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class MultiplyDoubleNegationStrategy() 
	: NumericBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.UnaryMinusExpression, SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    optimized = MultiplyExpression(context.Left.Syntax.Operand, context.Right.Syntax.Operand);
		return true;
	}
}
