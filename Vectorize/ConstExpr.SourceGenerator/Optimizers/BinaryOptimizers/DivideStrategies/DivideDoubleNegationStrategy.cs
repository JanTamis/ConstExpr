using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for double negation cancellation: (-x) / (-y) => x / y
/// </summary>
public class DivideDoubleNegationStrategy() 
	: NumericBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.UnaryMinusExpression, SyntaxKind.UnaryMinusExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax.Operand)
		    || !IsPure(context.Right.Syntax.Operand))
		{
			optimized = null;
			return false;
		}

		optimized = BinaryExpression(SyntaxKind.DivideExpression, context.Left.Syntax.Operand, context.Right.Syntax.Operand);
		return true;
	}
}
