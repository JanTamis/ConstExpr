using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for combining constant masks: (x ^ mask1) ^ mask2 => x ^ (mask1 ^ mask2)
/// </summary>
public class ExclusiveOrCombineMasksStrategy() : NumericOrBooleanBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.ExclusiveOrExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftXorRightValue)
		    || !TryCreateLiteral(leftXorRightValue.ExclusiveOr(context.Right.Syntax.Token.Value), out var combinedLiteral))
    {
      return false;
    }

    optimized = ExclusiveOrExpression(context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
