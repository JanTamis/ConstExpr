using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for left negation: (-x) * y => -(x * y) (pure)
/// Requires AssociativeMath for floating-point safety (rearranges operations).
/// </summary>
public class MultiplyNegationStrategy() : SymmetricStrategy<NumericBinaryStrategy, PrefixUnaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.UnaryMinusExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.AssociativeMath ];

	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		optimized = UnaryMinusExpression(
			ParenthesizedExpression(MultiplyExpression(
				context.Left.Syntax.Operand,
				context.Right.Syntax)));
		
		return true;
	}
}