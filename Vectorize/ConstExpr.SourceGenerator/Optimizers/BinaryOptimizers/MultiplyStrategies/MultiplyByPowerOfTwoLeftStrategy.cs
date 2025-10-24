using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: (power of two) * x => x << n (integer)
/// </summary>
public class MultiplyByPowerOfTwoLeftStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericPowerOfTwo(out _);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Value.IsNumericPowerOfTwo(out var power))
		{
			return BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
				LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		}

		return null;
	}
}
