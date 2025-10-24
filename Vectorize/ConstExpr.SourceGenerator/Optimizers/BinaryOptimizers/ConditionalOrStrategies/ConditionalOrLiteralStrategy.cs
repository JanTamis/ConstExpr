using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for literal boolean optimization: true || x = true, false || x = x or  x || false = x, x || true = true
/// </summary>
public class ConditionalOrLiteralStrategy : SymmetricStrategy<BooleanBinaryStrategy>
{
	public override bool CanBeOptimizedSymmetric(BinaryOptimizeContext context)
	{
		return context.Left is { HasValue: true, Value: bool };
	}

	public override SyntaxNode? OptimizeSymmetric(BinaryOptimizeContext context)
	{
		return context.Left switch
		{
			// true || x = true
			{ HasValue: true, Value: true } => SyntaxHelpers.CreateLiteral(true),
			// false || x = x
			{ HasValue: true, Value: false } => context.Right.Syntax,
			_ => null
		};
	}
}
