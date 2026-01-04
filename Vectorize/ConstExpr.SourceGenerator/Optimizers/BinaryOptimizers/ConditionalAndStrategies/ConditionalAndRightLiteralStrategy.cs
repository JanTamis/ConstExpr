using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for right-side literal boolean optimization: x && true = x, x && false = false (only if x is pure)
/// </summary>
public class ConditionalAndRightLiteralStrategy : BooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.TryGetLiteral(context.Right.Syntax, out var value)
		    || value is not bool
		    || !IsPure(context.Left.Syntax))
			return false;

		optimized = value switch
		{
			// x && true = x (only if x is pure)
			true => context.Left.Syntax,
			// x && false = false (only if x is pure)
			false => SyntaxHelpers.CreateLiteral(false),
		};

		return true;
	}
}