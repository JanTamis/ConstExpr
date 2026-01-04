using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLessThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LessThan;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new LessThanReflexiveStrategy();
	}
}