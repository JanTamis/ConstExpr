using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.UnsignedRightShiftStrategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryUnsignedRightShiftOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.UnsignedRightShift;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new UnsignedRightShiftByZeroStrategy();
		yield return new UnsignedRightShiftZeroStrategy();
		yield return new UnsignedRightShiftCombineStrategy();
	}
}