using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for redundancy elimination: (a && b) && a => a && b (already covered, pure)
/// </summary>
public class ConditionalAndRedundancyStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } rightAnd
				&& IsPure(context.Left.Syntax)
				&& (rightAnd.Left.IsEquivalentTo(context.Left.Syntax) || rightAnd.Right.IsEquivalentTo(context.Left.Syntax));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Right.Syntax;
	}
}
