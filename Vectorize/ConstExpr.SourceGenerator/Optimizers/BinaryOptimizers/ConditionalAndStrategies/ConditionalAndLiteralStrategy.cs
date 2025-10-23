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
		return context.Left is { HasValue: true, Value: false or true };
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Left switch
		{
			// false && x = false
			{ HasValue: true, Value: false } => SyntaxHelpers.CreateLiteral(false),
			// true && x = x
			{ HasValue: true, Value: true } => context.Right.Syntax,
			_ => null
		};

	}
}
