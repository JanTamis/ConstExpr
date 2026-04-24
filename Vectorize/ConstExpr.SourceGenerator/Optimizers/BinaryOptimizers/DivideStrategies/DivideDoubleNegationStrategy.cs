using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for double negation cancellation: (-x) / (-y) => x / y
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class DivideDoubleNegationStrategy() 
	: NumericBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.UnaryMinusExpression, SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax.Operand)
		    || !IsPure(context.Right.Syntax.Operand))
		{
			optimized = null;
			return false;
		}

		optimized = DivideExpression(context.Left.Syntax.Operand, context.Right.Syntax.Operand);
		return true;
	}
}
