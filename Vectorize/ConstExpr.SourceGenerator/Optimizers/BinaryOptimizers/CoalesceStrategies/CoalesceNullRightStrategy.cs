using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.CoalesceStrategies;

/// <summary>
///   x ?? null => x. If x is null the original expression evaluates to null (same as x), and if x is
///   non-null it evaluates to x - either way the result equals x. x is evaluated exactly once on both
///   sides, so there's no duplication/purity concern.
/// </summary>
public class CoalesceNullRightStrategy : BaseBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Right.Syntax.IsKind(SyntaxKind.NullLiteralExpression))
		{
			optimized = null;
			return false;
		}

		optimized = context.Left.Syntax;
		return true;
	}
}