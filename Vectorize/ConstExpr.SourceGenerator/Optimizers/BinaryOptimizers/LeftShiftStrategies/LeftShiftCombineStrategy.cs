using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for combining shifts: ((x << a) << b) => x << (a + b)
/// </summary>
public class LeftShiftCombineStrategy : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsKind(SyntaxKind.LeftShiftExpression)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || !SyntaxHelpers.TryGetLiteral(leftShiftValue.Add(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			return false;
		}

		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
