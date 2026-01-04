using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to addition: 2 * x => x + x (pure, non-integer)
/// </summary>
public class MultiplyByTwoToAdditionLeftStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    || !leftValue.IsNumericValue(2)
		    || !IsPure(context.Right.Syntax))
			return false;

		optimized = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, context.Right.Syntax, context.Right.Syntax));
		return true;
	}
}