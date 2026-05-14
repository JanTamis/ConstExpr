using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.UnsignedRightShiftStrategies;

/// <summary>
///   Strategy for combining shifts: (x &gt;&gt;&gt; a) &gt;&gt;&gt; b =&gt; x &gt;&gt;&gt; (a + b)
/// </summary>
public class UnsignedRightShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.UnsignedRightShiftExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || !TryCreateLiteral(context.Right.Syntax.Token.Value.Add(leftShiftValue), out var combinedLiteral))
		{
			return false;
		}

		optimized = BinaryExpression(SyntaxKind.UnsignedRightShiftExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}