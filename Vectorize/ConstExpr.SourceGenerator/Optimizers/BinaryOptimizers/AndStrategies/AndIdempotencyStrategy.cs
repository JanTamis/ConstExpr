using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Idempotency: x & x = x (for pure expressions)
/// </summary>
public class AndIdempotencyStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (base.TryOptimize(context, out optimized)
		    && LeftEqualsRight(context) 
		    && IsPure(context.Left.Syntax))
		{
			optimized = context.Left.Syntax;
			return true;
		}

		return false;
	}
}
