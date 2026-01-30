using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
/// Strategy for combining shifts: ((x >> a) >> b) => x >> (a + b)
/// </summary>
public class RightShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.RightShiftExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{ 
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || !SyntaxHelpers.TryGetLiteral(context.Right.Syntax.Token.Value.Add(leftShiftValue), out var combinedLiteral))
    {
      return false;
    }

    optimized = BinaryExpression(SyntaxKind.RightShiftExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
