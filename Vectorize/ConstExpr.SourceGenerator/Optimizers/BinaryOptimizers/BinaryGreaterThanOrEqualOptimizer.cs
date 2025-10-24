using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryGreaterThanOrEqualOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.GreaterThanOrEqual;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new GreaterThanOrEqualReflexiveStrategy();
	}
}

