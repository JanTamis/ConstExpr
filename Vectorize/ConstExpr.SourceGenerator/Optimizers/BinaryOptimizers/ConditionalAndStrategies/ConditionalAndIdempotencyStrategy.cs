using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for idempotency: x && x = x (for pure expressions)
/// </summary>
public class ConditionalAndIdempotencyStrategy : BooleanBinaryStrategy
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
