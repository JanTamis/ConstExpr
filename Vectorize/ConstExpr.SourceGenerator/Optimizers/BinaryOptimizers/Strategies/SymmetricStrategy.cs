using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SymmetricStrategy<TStrategy> : BaseBinaryStrategy
	where TStrategy : IBinaryStrategy, new()
{
	private readonly TStrategy _innerStrategy = new();

	public abstract bool CanBeOptimizedSymmetric(BinaryOptimizeContext context);
	public abstract SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context);

	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		var swappedContext = new BinaryOptimizeContext
		{
			Left = context.Right,
			Right = context.Left,
			Type = context.Type
		};

		return _innerStrategy.CanBeOptimized(context)
		       && (CanBeOptimizedSymmetric(context) || CanBeOptimizedSymmetric(swappedContext));
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		var swappedContext = new BinaryOptimizeContext
		{
			Left = context.Right,
			Right = context.Left,
			Type = context.Type
		};

		if (CanBeOptimizedSymmetric(context))
		{
			return OptimizeSymmetric(context);
		}

		if (CanBeOptimizedSymmetric(swappedContext))
		{
			return OptimizeSymmetric(swappedContext);
		}

		return null;
	}
}