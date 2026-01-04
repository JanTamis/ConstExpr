using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by negative one: x * -1 = -x and -1 * x = -x
/// </summary>
public class MultiplyByNegativeOneStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.HasValue && context.Right.Value.IsNumericNegativeOne();
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Left.Syntax);
	}
}
