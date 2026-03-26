using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Combine masks: (x & mask1) & mask2 => x & (mask1 & mask2)
/// </summary>
public class AndCombineMasksStrategy() : SymmetricStrategy<NumericOrBooleanBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseAndExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.TryGetValue(context.Left.Syntax.Right, out var leftMask)
		    || !TryCreateLiteral(leftMask.And(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			optimized = null;
			return false;
		}
		
		optimized = BitwiseAndExpression(context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
