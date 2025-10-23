using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for absorption law: a && (a || b) => a and (a || b) && a => a (pure)
/// </summary>
public class ConditionalAndAbsorptionStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		// a && (a || b) => a (pure)
		if (context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } rightOr
				&& IsPure(context.Left.Syntax))
		{
			if (rightOr.Left.IsEquivalentTo(context.Left.Syntax) || rightOr.Right.IsEquivalentTo(context.Left.Syntax))
			{
				return true;
			}
		}

		// (a || b) && a => a (pure)
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } leftOr
				&& IsPure(context.Right.Syntax))
		{
			if (leftOr.Left.IsEquivalentTo(context.Right.Syntax) || leftOr.Right.IsEquivalentTo(context.Right.Syntax))
			{
				return true;
			}
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// a && (a || b) => a (pure)
		if (context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } rightOr
				&& IsPure(context.Left.Syntax))
		{
			if (rightOr.Left.IsEquivalentTo(context.Left.Syntax) || rightOr.Right.IsEquivalentTo(context.Left.Syntax))
			{
				return context.Left.Syntax;
			}
		}

		// (a || b) && a => a (pure)
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } leftOr
			&& IsPure(context.Right.Syntax))
		{
			if (leftOr.Left.IsEquivalentTo(context.Right.Syntax) || leftOr.Right.IsEquivalentTo(context.Right.Syntax))
			{
				return context.Right.Syntax;
			}
		}

		return null;
	}
}
