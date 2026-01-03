using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to shift: x * 2 => x << 1 (integer)
/// </summary>
public class MultiplyByTwoToShiftRightStrategy : IntegerBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || !rightValue.IsNumericValue(2))
			return false;
		
		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Left.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
		return true;
	}
}
