using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryDivideOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Divide;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new DivideConstantFoldingStrategy();
		yield return new DivideByOneStrategy();
		yield return new DivideByNegativeOneStrategy();
		yield return new DivideZeroByNonZeroStrategy();
		yield return new DivideIdempotencyStrategy();
		yield return new DivideByPowerOfTwoToShiftStrategy();
		yield return new DivideToMultiplyReciprocalStrategy();
		yield return new DivideMultiplySimplificationStrategy();
		yield return new DivideDoubleNegationStrategy();
		yield return new DivideLeftNegationStrategy();
		yield return new DivideRightNegationStrategy();
		yield return new DivideOneToReciprocalStrategy();
	}

	//public override bool TryOptimize(MetadataLoader loader, IDictionary<string, VariableItem> variables, out SyntaxNode? result)
	//{
	//	result = null;

	//	if (!Type.IsNumericType())
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
