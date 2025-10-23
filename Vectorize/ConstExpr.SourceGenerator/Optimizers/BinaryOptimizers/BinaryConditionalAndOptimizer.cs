using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalAnd;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ConditionalAndLiteralStrategy();
		yield return new ConditionalAndRightLiteralStrategy();
		yield return new ConditionalAndAbsorptionStrategy();
		yield return new ConditionalAndRedundancyStrategy();
		yield return new ConditionalAndContradictionStrategy();
		yield return new ConditionalAndIdempotencyStrategy();
	}

	// public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	// {
	// 	result = null;
	//
	// 	if (!Type.IsBoolType())
	// 	{
	// 		return false;
	// 	}
	//
	// 	Left.TryGetLiteralValue(loader, variables, out var leftValue);
	// 	Right.TryGetLiteralValue(loader, variables, out var rightValue);
	//
	// 	var context = new BinaryOptimizeContext
	// 	{
	// 		Left = Left,
	// 		LeftType = LeftType,
	// 		HasLeftValue = leftValue != null,
	// 		LeftValue = leftValue,
	// 		Right = Right,
	// 		RightType = RightType,
	// 		HasRightValue = rightValue != null,
	// 		RightValue = rightValue,
	// 		Type = Type
	// 	};
	//
	// 	// Try each strategy
	// 	foreach (var strategy in Strategies)
	// 	{
	// 		if (strategy.CanBeOptimized(context))
	// 		{
	// 			// Special case for ConditionalAndIdempotencyStrategy which needs variables
	// 			if (strategy is ConditionalAndIdempotencyStrategy)
	// 			{
	// 				var idempotencyStrategy = new ConditionalAndIdempotencyStrategy(variables);
	// 				if (idempotencyStrategy.CanBeOptimized(context))
	// 				{
	// 					result = idempotencyStrategy.Optimize(context);
	// 					return true;
	// 				}
	// 			}
	// 			else
	// 			{
	// 				result = strategy.Optimize(context);
	// 				return true;
	// 			}
	// 		}
	// 	}
	//
	// 	return false;
	// }
}

