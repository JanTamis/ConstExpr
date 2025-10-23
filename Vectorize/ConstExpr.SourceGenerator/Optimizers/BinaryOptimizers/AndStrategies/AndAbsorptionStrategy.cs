using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Absorption with Or: x & (x | y) = x and (x | y) & x = x (pure)
/// </summary>
public class AndAbsorptionStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		// Right is (x | y)
		return context.Right.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseOrExpression } rightBin 
		       && IsPure(context.Left.Syntax) 
		       && IsPure(rightBin.Left) 
		       && IsPure(rightBin.Right) 
		       && (context.Left.Syntax.IsEquivalentTo(rightBin.Left) || context.Left.Syntax.IsEquivalentTo(rightBin.Right));

	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
