using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinarySubtractOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Subtract;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new SubtractIdentityElementStrategy();
		yield return new SubtractZeroMinusStrategy();
		yield return new SubtractIdempotencyStrategy();
		yield return new SubtractDoubleNegationStrategy();
		yield return new SubtractAdditionCancellationRightStrategy();
		yield return new SubtractAdditionCancellationLeftStrategy();
		yield return new SubtractFMALeftMultiplyStrategy();
		yield return new SubtractFMARightMultiplyStrategy();
	}
}