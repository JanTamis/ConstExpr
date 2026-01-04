using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalOr;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ConditionalOrLiteralStrategy();
		yield return new ConditionalOrIdempotencyStrategy();
		yield return new ConditionalOrAbsorptionStrategy();
		yield return new ConditionalOrAbsorptionOrStrategy();
		yield return new ConditionalOrTautologyStrategy();
		yield return new ConditionalOrDeMorganStrategy();
		yield return new ConditionalOrIsNullOrEmptyStrategy();

		yield return new ConditionalPatternStrategy();
		yield return new ConditionalPatternCombinerStrategy();
		yield return new ConditionalPatternCombinersStrategy();
	}

	//public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	//{
	//	result = null;

	//	if (!Type.IsBoolType())
	//	{
	//		return false;
	//	}

	//	var leftValue = Left.TryGetLiteralValue(loader, variables, out var lv) ? lv : null;
	//	var rightValue = Right.TryGetLiteralValue(loader, variables, out var rv) ? rv : null;

	//	var context = new BinaryOptimizeContext
	//	{
	//		Left = new BinaryOptimizeElement
	//		{
	//			Syntax = Left,
	//			Type = LeftType,
	//			HasValue = leftValue is not null,
	//			Value = leftValue
	//		},
	//		Right = new BinaryOptimizeElement
	//		{
	//			Syntax = Right,
	//			Type = RightType,
	//			HasValue = rightValue is not null,
	//			Value = rightValue
	//		},
	//		Type = Type
	//	};

	//	foreach (var strategy in GetStrategies())
	//	{
	//		if (strategy.CanBeOptimized(context))
	//		{
	//			result = strategy.Optimize(context);
	//			if (result != null)
	//			{
	//				return true;
	//			}
	//		}
	//	}

	//	return false;
	//}
}
