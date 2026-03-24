using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;

/// <summary>
/// Strategy for boolean true: x ^ true = !x and true ^ x = !x
/// </summary>
public class ExclusiveOrBooleanTrueStrategy : SymmetricStrategy<BooleanBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (context.Left.Syntax.Token.Value is not true)
		{
			optimized = null;
			return false;
		}

		optimized = LogicalNotExpression(context.Right.Syntax);
		return true;
	}
}
