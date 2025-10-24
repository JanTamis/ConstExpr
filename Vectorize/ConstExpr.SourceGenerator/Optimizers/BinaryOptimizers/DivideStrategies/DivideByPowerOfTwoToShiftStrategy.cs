using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by power of two: x / (2^n) => x >> n (unsigned integers only)
/// </summary>
public class DivideByPowerOfTwoToShiftStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Type.IsUnsignedInteger() 
		       && context.Right is { HasValue: true, Value: { } value } 
		       && value.IsNumericPowerOfTwo(out _);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Right.Value.IsNumericPowerOfTwo(out var power))
		{
			return null;
		}

		return BinaryExpression(
			SyntaxKind.RightShiftExpression, 
			context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
	}
}
