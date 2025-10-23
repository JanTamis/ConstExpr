using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;

/// <summary>
/// Strategy for subtraction cancellation optimization:
/// (x - a) + a => x (algebraic identity, pure)
/// a + (x - a) => x (algebraic identity, pure)
/// </summary>
public class AddSubtractionCancellationStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression } leftSub
		       && leftSub.Right.IsEquivalentTo(context.Right.Syntax)
		       && IsPure(leftSub.Left) && IsPure(leftSub.Right) && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		var leftSub = (BinaryExpressionSyntax) context.Left.Syntax;
		return leftSub.Left;
	}
}