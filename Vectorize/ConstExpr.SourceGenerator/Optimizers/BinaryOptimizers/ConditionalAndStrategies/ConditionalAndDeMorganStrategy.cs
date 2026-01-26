using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for De Morgan's law: !a && !b → !(a || b)
/// This can reduce the number of negations and may help branch prediction.
/// </summary>
public class ConditionalAndDeMorganStrategy() 
	: BooleanBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.ExclamationToken, SyntaxKind.ExclamationToken)
{
	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
    {
      return false;
    }

    optimized = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
			ParenthesizedExpression(BinaryExpression(SyntaxKind.LogicalOrExpression,
				context.Left.Syntax.Operand,
				context.Right.Syntax.Operand)));

		return true;
	}
}