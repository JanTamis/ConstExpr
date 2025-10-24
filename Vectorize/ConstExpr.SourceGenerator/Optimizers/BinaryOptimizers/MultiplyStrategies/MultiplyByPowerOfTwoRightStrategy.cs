using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: x * (power of two) => x << n (integer)
/// </summary>
public class MultiplyByPowerOfTwoRightStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericPowerOfTwo(out _);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Right.Value.IsNumericPowerOfTwo(out var power))
		{
			return BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}

		return null;
	}
}
