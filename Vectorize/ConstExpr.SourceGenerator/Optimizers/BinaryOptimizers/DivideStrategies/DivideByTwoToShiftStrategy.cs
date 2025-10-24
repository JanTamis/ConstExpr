using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by two: x / 2 => x >> 1 (unsigned integers only)
/// </summary>
public class DivideByTwoToShiftStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Type.IsUnsignedInteger() 
		       && context.Right is { HasValue: true, Value: { } value } 
		       && value.IsNumericValue(2);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return BinaryExpression(
			SyntaxKind.RightShiftExpression, 
			context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
	}
}
