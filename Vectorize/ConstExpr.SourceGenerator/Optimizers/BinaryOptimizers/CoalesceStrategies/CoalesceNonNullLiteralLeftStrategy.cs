using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.CoalesceStrategies;

/// <summary>
///   literal ?? x => literal, when the literal can never be null. The left operand of '??' must be a
///   reference type or a nullable value type, so a bare char/numeric/bool literal (all non-nullable
///   value types) can never reach here uncast - only a string literal (string is a reference type)
///   legally appears in this position without a cast, so that's the only kind checked. A cast like
///   <c>(int?)5 ?? x</c> arrives as a CastExpressionSyntax, not a LiteralExpressionSyntax, so it
///   never matches this strategy's TLeft anyway. <c>default</c> is deliberately excluded even though
///   it parses as a LiteralExpressionSyntax too - it IS null when the target is a reference type.
/// </summary>
public class CoalesceNonNullLiteralLeftStrategy : BaseBinaryStrategy<LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.StringLiteralExpression))
		{
			optimized = null;
			return false;
		}

		optimized = context.Left.Syntax;
		return true;
	}
}