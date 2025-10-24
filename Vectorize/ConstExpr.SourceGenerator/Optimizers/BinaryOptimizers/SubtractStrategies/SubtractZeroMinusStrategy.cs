using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for zero minus optimization: 0 - x = -x
/// </summary>
public class SubtractZeroMinusStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericZero();
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Right.Syntax);
	}
}
