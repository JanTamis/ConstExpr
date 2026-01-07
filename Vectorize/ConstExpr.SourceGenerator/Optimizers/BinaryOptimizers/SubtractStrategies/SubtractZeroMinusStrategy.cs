using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for zero minus optimization: 0 - x = -x
/// </summary>
public class SubtractZeroMinusStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (base.TryOptimize(context, out optimized)
		    || context.TryGetValue(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericZero())
			return false;

		optimized = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Right.Syntax);
		return true;
	}
}
