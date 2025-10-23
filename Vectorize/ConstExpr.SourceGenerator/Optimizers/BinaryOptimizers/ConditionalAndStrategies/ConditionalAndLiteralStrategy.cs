using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for literal boolean optimization: false && x = false, true && x = x
/// </summary>
public class ConditionalAndLiteralStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		// false && x = false
		if (context.Left.HasValue && context.Left.Value is false)
		{
			return true;
		}

		// true && x = x
		if (context.Left.HasValue && context.Left.Value is true)
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		// false && x = false
		if (context.Left.HasValue && context.Left.Value is false)
		{
			return SyntaxHelpers.CreateLiteral(false);
		}

		// true && x = x
		if (context.Left.HasValue && context.Left.Value is true)
		{
			return context.Right.Syntax;
		}

		return null;
	}
}
