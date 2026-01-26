using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for double negated addition: -x + (-y) => -(x + y) (pure)
/// </summary>
public class AddDoubleNegatedStrategy() : NumericBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.UnaryMinusExpression, SyntaxKind.UnaryMinusExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !IsPure(context.Left.Syntax.Operand)
		    || !IsPure(context.Right.Syntax.Operand))
    {
      return false;
    }

    optimized = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression,
					context.Left.Syntax.Operand,
					context.Right.Syntax.Operand)));

		return true;

	}
}