using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for right-side literal boolean optimization: x && true = x, x && false = false (only if x is pure)
/// </summary>
public class ConditionalAndRightLiteralStrategy : BooleanBinaryStrategy<ExpressionSyntax, LiteralExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, LiteralExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !IsPure(context.Left.Syntax))
			return false;

		optimized = context.Right.Syntax.Token.Value switch
		{
			// x && true = x (only if x is pure)
			true => context.Left.Syntax,
			// x && false = false (only if x is pure)
			false => SyntaxHelpers.CreateLiteral(false),
			_ => null,
		};

		return true;
	}
}