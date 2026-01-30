using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for double negation: x - -y => x + y (pure)
/// </summary>
public class SubtractDoubleNegationStrategy() : NumericBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.UnaryMinusExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
    {
      return false;
    }

    optimized = BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax, context.Right.Syntax.Operand);
		return true;
	}
}
