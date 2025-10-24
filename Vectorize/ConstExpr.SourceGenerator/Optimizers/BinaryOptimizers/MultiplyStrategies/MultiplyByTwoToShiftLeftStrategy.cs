using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to shift: 2 * x => x << 1 (integer)
/// </summary>
public class MultiplyByTwoToShiftLeftStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericValue(2);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
	}
}
