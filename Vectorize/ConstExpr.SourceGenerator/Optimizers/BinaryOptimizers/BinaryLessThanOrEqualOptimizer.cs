using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryLessThanOrEqualOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.LessThanOrEqual;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new LessThanOrEqualReflexiveStrategy();
	}
}