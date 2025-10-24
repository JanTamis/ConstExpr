using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for absorption law: x | (x & y) = x and (x & y) | x = x (pure)
/// </summary>
public class OrAbsorptionStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		if (!base.CanBeOptimized(context))
			return false;

		// x | (x & y) = x
		if (context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andRight
		    && IsPure(context.Left.Syntax) && IsPure(andRight.Left) && IsPure(andRight.Right))
		{
			return context.Left.Syntax.IsEquivalentTo(andRight.Left) 
			       || context.Left.Syntax.IsEquivalentTo(andRight.Right);
		}

		// (x & y) | x = x
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andLeft
		    && IsPure(context.Right.Syntax) && IsPure(andLeft.Left) && IsPure(andLeft.Right))
		{
			return context.Right.Syntax.IsEquivalentTo(andLeft.Left) 
			       || context.Right.Syntax.IsEquivalentTo(andLeft.Right);
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// x | (x & y) = x
		if (context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andRight
		    && (context.Left.Syntax.IsEquivalentTo(andRight.Left) || context.Left.Syntax.IsEquivalentTo(andRight.Right)))
		{
			return context.Left.Syntax;
		}

		// (x & y) | x = x
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } andLeft
		    && (context.Right.Syntax.IsEquivalentTo(andLeft.Left) || context.Right.Syntax.IsEquivalentTo(andLeft.Right)))
		{
			return context.Right.Syntax;
		}

		return null;
	}
}
