using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to shift: x * 2 => x << 1 (integer)
/// </summary>
public class MultiplyByTwoToShiftRightStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericValue(2);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
	}
}
