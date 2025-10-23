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
		// x && true = x (only if x is pure)
		if (context.Right.HasValue && context.Right.Value is true && IsPure(context.Left.Syntax))
		{
			return true;
		}

		// x && false = false (only if x is pure)
		if (context.Right.HasValue && context.Right.Value is false && IsPure(context.Left.Syntax))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// x && true = x (only if x is pure)
		if (context.Right.HasValue && context.Right.Value is true && IsPure(context.Left.Syntax))
		{
			return context.Left.Syntax;
		}

		// x && false = false (only if x is pure)
		if (context.Right.HasValue && context.Right.Value is false && IsPure(context.Left.Syntax))
		{
			return SyntaxHelpers.CreateLiteral(false);
		}

		return null;
	}
}
