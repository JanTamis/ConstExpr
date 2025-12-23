using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThan;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new GreaterThanReflexiveStrategy();
	}
}
