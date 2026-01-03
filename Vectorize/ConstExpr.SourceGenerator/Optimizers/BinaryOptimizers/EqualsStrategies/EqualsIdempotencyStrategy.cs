using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for reflexive equality: x == x = true (for pure expressions)
/// </summary>
public class EqualsIdempotencyStrategy : BooleanBinaryStrategy<ExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !LeftEqualsRight(context)
		    || !IsPure(context.Left.Syntax))
			return false;

		optimized = SyntaxHelpers.CreateLiteral(true);
		return true;
	}
}
