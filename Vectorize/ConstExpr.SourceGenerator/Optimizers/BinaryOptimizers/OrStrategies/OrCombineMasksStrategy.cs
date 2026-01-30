using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for combining constant masks: (x | mask1) | mask2 => x | (mask1 | mask2)
/// </summary>
public class OrCombineMasksStrategy() : SymmetricStrategy<NumericOrBooleanBinaryStrategy, BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.BitwiseOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.TryGetValue(context.Left.Syntax.Right, out var leftMask)
		    || !SyntaxHelpers.TryGetLiteral(leftMask.Or(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			optimized = null;
			return false;
		}

		optimized = BinaryExpression(SyntaxKind.BitwiseOrExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
