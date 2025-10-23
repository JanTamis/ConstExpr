using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public interface IBinaryStrategy
{
	bool CanBeOptimized(BinaryOptimizeContext context);

	SyntaxNode? Optimize(BinaryOptimizeContext context);
}
