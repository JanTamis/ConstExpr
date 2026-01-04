using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to addition: x * 2 => x + x (pure, non-integer)
/// </summary>
public class MultiplyByTwoToAdditionRightStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || !rightValue.IsNumericValue(2)
		    || !IsPure(context.Left.Syntax))
			return false;
		
		optimized = ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax, context.Left.Syntax));
		return true;
	}
}
