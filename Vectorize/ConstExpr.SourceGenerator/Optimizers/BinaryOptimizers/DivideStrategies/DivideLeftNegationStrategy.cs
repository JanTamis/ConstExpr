using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for left negation extraction: (-x) / y => -(x / y)
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class DivideLeftNegationStrategy() : NumericBinaryStrategy<PrefixUnaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax.Operand)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = UnaryMinusExpression(
			ParenthesizedExpression(
				DivideExpression(context.Left.Syntax.Operand, context.Right.Syntax)));
			
		return true;
	}
}
