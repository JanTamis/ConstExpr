using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;

/// <summary>
/// Strategy for reflexive comparison: x > x => false (pure)
/// </summary>
public class GreaterThanReflexiveStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return LeftEqualsRight(context) 
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return SyntaxHelpers.CreateLiteral(false);
	}
}
