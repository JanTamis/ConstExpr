using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryMultiplyOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Multiply;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new MultiplyByZeroStrategy();
		yield return new MultiplyIdentityElementStrategy();
		yield return new MultiplyByNegativeOneStrategy();
		yield return new MultiplyConstantFoldingStrategy();
		// yield return new MultiplyByTwoToShiftRightStrategy();
		yield return new MultiplyByTwoToShiftStrategy();
		yield return new MultiplyByTwoToAdditionStrategy();
		// yield return new MultiplyStrengthReductionRightStrategy();
		// yield return new MultiplyStrengthReductionLeftStrategy();
		yield return new MultiplyByPowerOfTwoRightStrategy();
		yield return new MultiplyByPowerOfTwoLeftStrategy();
		yield return new MultiplyDoubleNegationStrategy();
		yield return new MultiplyNegationStrategy();
		// yield return new MultiplyRightNegationStrategy();
	}
}