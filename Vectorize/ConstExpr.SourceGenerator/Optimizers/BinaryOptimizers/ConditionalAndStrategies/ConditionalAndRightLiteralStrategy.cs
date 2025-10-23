using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for right-side literal boolean optimization: x && true = x, x && false = false (only if x is pure)
/// </summary>
public class ConditionalAndRightLiteralStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Right is { HasValue: true, Value: true or false } && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Right switch
		{
			// x && true = x (only if x is pure)
			{ HasValue: true, Value: true } when IsPure(context.Left.Syntax) => context.Left.Syntax,
			// x && false = false (only if x is pure)
			{ HasValue: true, Value: false } when IsPure(context.Left.Syntax) => SyntaxHelpers.CreateLiteral(false),
			_ => null
		};

	}
}
