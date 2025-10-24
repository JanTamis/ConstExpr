using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for absorption law: a || (a && b) => a (pure) or (a && b) || a => a (pure)
/// </summary>
public class ConditionalOrAbsorptionStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } rightAnd
					 && (rightAnd.Left.IsEquivalentTo(context.Left.Syntax) || rightAnd.Right.IsEquivalentTo(context.Left.Syntax))
					 && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
