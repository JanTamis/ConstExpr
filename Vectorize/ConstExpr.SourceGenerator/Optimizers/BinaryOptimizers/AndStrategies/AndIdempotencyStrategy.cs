using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// Idempotency: x & x = x (for pure expressions)
/// </summary>
public class AndIdempotencyStrategy : NumericOrBooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return LeftEqualsRight(context) && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
