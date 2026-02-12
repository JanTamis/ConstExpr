using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// (x | mask1) & mask2 when mask1 & mask2 == 0 => x & mask2 (when x is pure)
/// symmetric
/// </summary>
public class AndOrMaskIntersectionZeroStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax.Left)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftValue)
		    || !Equals(context.Right.Syntax.Token.Value.And(leftValue), 0.ToSpecialType(context.Type.SpecialType)))
		{
			optimized = null;
			return false;
		}
		
		optimized = BinaryExpression(SyntaxKind.BitwiseAndExpression, context.Left.Syntax.Left, context.Right.Syntax);
		return true;
	}
}
