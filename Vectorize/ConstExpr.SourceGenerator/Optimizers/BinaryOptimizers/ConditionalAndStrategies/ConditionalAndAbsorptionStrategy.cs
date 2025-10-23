using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for absorption law: a && (a || b) => a and (a || b) && a => a (pure)
/// </summary>
public class ConditionalAndAbsorptionStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } rightOr
		       && IsPure(context.Left.Syntax)
		       && (rightOr.Left.IsEquivalentTo(context.Left.Syntax) || rightOr.Right.IsEquivalentTo(context.Left.Syntax));
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax;
	}
}
