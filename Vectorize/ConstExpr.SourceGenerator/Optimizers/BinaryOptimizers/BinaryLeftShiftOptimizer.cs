using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LeftShiftStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLeftShiftOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LeftShift;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new LeftShiftByZeroStrategy();
		yield return new LeftShiftZeroStrategy();
		yield return new LeftShiftCombineStrategy();
	}
}