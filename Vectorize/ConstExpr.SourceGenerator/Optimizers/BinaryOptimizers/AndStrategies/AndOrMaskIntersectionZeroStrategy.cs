using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ConstExpr.SourceGenerator.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// (x | mask1) & mask2 when mask1 & mask2 == 0 => x & mask2 (when x is pure)
/// symmetric
/// </summary>
public class AndOrMaskIntersectionZeroStrategy : SymmetricStrategy<NumericBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } leftOr)
		{
			if (!IsPure(leftOr.Left))
			{
				return false;
			}

			if (context.Right.HasValue && context.Left.Value != null)
			{
				// Check if mask1 & mask2 == 0 using ObjectExtensions.And
				var mask1 = context.Left.Value;
				var mask2 = context.Right.Value;
				var andResult = mask1.And(mask2);

				return andResult is not null && Equals(andResult, 0.ToSpecialType(context.Type.SpecialType));
			}
		}

		return false;
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } leftOr 
		    && context.Right.HasValue 
		    && context.Left.Value != null)
		{
			var mask2 = context.Right.Value;
			
			if (SyntaxHelpers.TryGetLiteral(mask2, out var maskLiteral))
			{
				return BinaryExpression(SyntaxKind.BitwiseAndExpression, leftOr.Left, maskLiteral);
			}
		}

		return null;
	}
}
