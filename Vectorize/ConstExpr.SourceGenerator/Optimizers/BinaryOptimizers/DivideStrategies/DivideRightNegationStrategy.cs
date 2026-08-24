using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
///   Strategy for right negation extraction: x / (-y) => -(x / y)
///   Safe under Strict (pure algebraic identity).
/// </summary>
public class DivideRightNegationStrategy() : NumericBinaryStrategy<ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax.Operand))
		{
			optimized = null;
			return false;
		}

		var divideExpression = DivideExpression(context.Left.Syntax, context.Right.Syntax.Operand);
		var parenthesizedDivideExpression = ParenthesizedExpression(context.Visit(divideExpression) ?? divideExpression);

		optimized = UnaryMinusExpression(context.Visit(parenthesizedDivideExpression) ?? parenthesizedDivideExpression);

		return true;
	}
}