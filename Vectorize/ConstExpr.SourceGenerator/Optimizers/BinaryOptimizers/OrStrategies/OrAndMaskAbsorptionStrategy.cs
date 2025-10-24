using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for mask absorption: (x & mask) | mask => mask (when x is pure)
/// </summary>
public class OrAndMaskAbsorptionStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.HasValue 
		       && context.Right.Value != null
		       && context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseAndExpression } leftAnd
		       && IsPure(leftAnd.Left)
		       && leftAnd.Right is LiteralExpressionSyntax leftAndRightLiteral
		       && EqualityComparer<object?>.Default.Equals(leftAndRightLiteral.Token.Value, context.Right.Value);
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Right.Syntax;
	}
}
