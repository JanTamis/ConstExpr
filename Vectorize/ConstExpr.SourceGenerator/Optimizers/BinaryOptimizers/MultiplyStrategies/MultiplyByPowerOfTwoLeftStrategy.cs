using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for power of two optimization: (power of two) * x => x << n (integer)
/// </summary>
public class MultiplyByPowerOfTwoLeftStrategy : IntegerBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericPowerOfTwo(out var power))
			return false;
		
		optimized = BinaryExpression(SyntaxKind.LeftShiftExpression, context.Right.Syntax,
			LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(power)));
		return true;
	}
}
