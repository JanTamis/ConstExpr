using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to shift: 2 * x => x << 1 (integer)
/// </summary>
public class MultiplyByTwoToShiftLeftStrategy : IntegerBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericValue(2))
			return false;
		
		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));
		return true;
	}
}
