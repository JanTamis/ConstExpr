using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for power of two optimization: x % (power of two) => x & (power - 1) (unsigned integers)
/// </summary>
public class ModuloByPowerOfTwoStrategy : IntegerBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context)
		       && context.Type.IsUnsignedInteger()
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericPowerOfTwo(out _);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Right.Value.IsNumericPowerOfTwo(out var power))
		{
			var mask = (1 << power) - 1;
			var maskLiteral = Helpers.SyntaxHelpers.CreateLiteral(mask.ToSpecialType(context.Type.SpecialType));

			if (maskLiteral != null)
			{
				return BinaryExpression(SyntaxKind.BitwiseAndExpression, context.Left.Syntax, maskLiteral);
			}
		}

		return null;
	}
}
