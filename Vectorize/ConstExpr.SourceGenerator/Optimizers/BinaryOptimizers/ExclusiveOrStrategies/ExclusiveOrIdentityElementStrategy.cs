using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for identity element: x ^ 0 = x and 0 ^ x = x
/// </summary>
public class ExclusiveOrIdentityElementStrategy : NumericBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
			return false;

		if (context.TryGetLiteral(context.Left.Syntax, out var leftValue)
		    && leftValue.IsNumericZero())
		{
			optimized = context.Right.Syntax;
			return true;
		}
		
		if (context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    && rightValue.IsNumericZero())
		{
			optimized = context.Left.Syntax;
			return true;
		}
		
		return false;
	}
}
