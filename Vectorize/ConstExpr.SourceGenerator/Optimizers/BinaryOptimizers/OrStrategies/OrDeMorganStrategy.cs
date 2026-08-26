using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
///   Strategy for bitwise De Morgan's law: ~a | ~b => ~(a &amp; b).
///   Reduces two NOTs + an OR to an AND + a single NOT, and exposes the inner AND to further
///   folding (e.g. SelfComplement, IdentityElement). Integer-only: '~' is invalid on bool.
/// </summary>
public class OrDeMorganStrategy()
	: IntegerBinaryStrategy<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.BitwiseNotExpression, SyntaxKind.BitwiseNotExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized))
		{
			return false;
		}

		optimized = BitwiseNotExpression(
			ParenthesizedExpression(BitwiseAndExpression(
				context.Left.Syntax.Operand,
				context.Right.Syntax.Operand)));

		return true;
	}
}