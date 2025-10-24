using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;

/// <summary>
/// Strategy for idempotency: x | x = x (pure)
/// </summary>
public class OrIdempotencyStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return LeftEqualsRight(context) 
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Left.Syntax;
	}
}
