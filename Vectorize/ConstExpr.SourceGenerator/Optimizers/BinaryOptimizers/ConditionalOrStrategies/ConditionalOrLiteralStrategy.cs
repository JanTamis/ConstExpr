using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for literal boolean optimization: true || x = true, false || x = x or  x || false = x, x || true = true
/// </summary>
public class ConditionalOrLiteralStrategy : SymmetricStrategy<BooleanBinaryStrategy, LiteralExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<LiteralExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		switch (context.Left.Syntax.Token.Value)
		{
			case true:
			{
				// true || x = true
				optimized = SyntaxHelpers.CreateLiteral(true);
				return true;
			}
			case false:
			{
				// false || x = x
				optimized = context.Right.Syntax;
				return true;
			}
			default:
			{
				optimized = null;
				return false;
			}
		}
	}
}