using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using ConstExpr.SourceGenerator.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Combine masks: (x & mask1) & mask2 => x & (mask1 & mask2)
/// </summary>
public class AndCombineMasksStrategy : SymmetricStrategy<NumericOrBooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.BitwiseAndExpression } 
		    && context.Right.HasValue 
		    && context.Left.Value != null)
		{
			var leftMask = context.Left.Value;
			var rightMask = context.Right.Value;
			var combined = leftMask.And(rightMask);

			if (combined != null && SyntaxHelpers.TryGetLiteral(combined, out _))
			{
				return true;
			}
		}

		return false;
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is BinaryExpressionSyntax leftAnd)
		{
			var leftMask = context.Left.Value;
			var rightMask = context.Right.Value;
			var combined = leftMask.And(rightMask);
			
			if (SyntaxHelpers.TryGetLiteral(combined, out var combinedLiteral))
			{
				return BinaryExpression(SyntaxKind.BitwiseAndExpression, leftAnd.Left, combinedLiteral);
			}
		}

		return null;
	}
}
