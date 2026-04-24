using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;

/// <summary>
/// Strategy for combining shifts: ((x << a) << b) => x << (a + b)
/// Safe under Strict (integer shift arithmetic).
/// </summary>
public class LeftShiftCombineStrategy() : IntegerBinaryStrategy<BinaryExpressionSyntax, LiteralExpressionSyntax>(leftKind: SyntaxKind.LeftShiftExpression)
{
	public override FastMathFlags[] RequiredFlags => [ FastMathFlags.Strict ];

	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || !TryCreateLiteral(leftShiftValue.Add(context.Right.Syntax.Token.Value), out var combinedLiteral))
		{
			return false;
		}

		optimized = LeftShiftExpression(context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
