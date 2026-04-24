using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for negated subtraction optimization:
/// x + (-y) => x - y (pure)
/// -x + y => y - x (pure)
/// Safe under Strict (pure algebraic identity).
/// </summary>
public class AddNegatedSubtractionStrategy() : SymmetricStrategy<NumericBinaryStrategy, ExpressionSyntax, PrefixUnaryExpressionSyntax>(rightKind: SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}

		optimized = SubtractExpression(context.Left.Syntax, context.Right.Syntax.Operand);
		return true;
	}
}