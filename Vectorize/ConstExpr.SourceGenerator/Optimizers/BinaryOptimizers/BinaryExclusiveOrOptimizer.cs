using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ExclusiveOrStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryExclusiveOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ExclusiveOr;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ExclusiveOrIdentityElementStrategy();
		yield return new ExclusiveOrSelfCancellationStrategy();
		yield return new ExclusiveOrAllBitsSetStrategy();
		yield return new ExclusiveOrBooleanTrueStrategy();
		yield return new ExclusiveOrAssociativeCancellationStrategy();
		yield return new ExclusiveOrCombineMasksStrategy();
	}
}
