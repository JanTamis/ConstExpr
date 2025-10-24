using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryRightShiftOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.RightShift;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new RightShiftByZeroStrategy();
		yield return new RightShiftZeroStrategy();
		yield return new RightShiftCombineStrategy();
	}
}
