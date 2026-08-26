using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
///   Strategy for bitwise De Morgan's law: ~a &amp; ~b => ~(a | b).
///   Reduces two NOTs + an AND to an OR + a single NOT, and exposes the inner OR to further
///   folding (e.g. AllBitsSet, IdentityElement). Integer-only: '~' is invalid on bool.
/// </summary>
public class AndDeMorganStrategy()
	: IntegerBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.BitwiseNotExpression, SyntaxKind.BitwiseNotExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		optimized = BitwiseNotExpression(
			ParenthesizedExpression(BitwiseOrExpression(
				context.Left.Syntax.Operand,
				context.Right.Syntax.Operand)));

		return true;
	}
}