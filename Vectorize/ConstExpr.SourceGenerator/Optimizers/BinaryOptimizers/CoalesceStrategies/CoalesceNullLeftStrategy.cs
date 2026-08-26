using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.CoalesceStrategies;

/// <summary>
///   null ?? x => x. The left operand is always null, so the right operand is always the result.
/// </summary>
public class CoalesceNullLeftStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.NullLiteralExpression))
		{
			optimized = null;
			return false;
		}

		optimized = context.Right.Syntax;
		return true;
	}
}