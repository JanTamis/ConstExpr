using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public interface IBinaryStrategy<TLeft, TRight> : IBinaryStrategy
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	bool TryOptimize(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized);
}

public interface IBinaryStrategy;