using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.OrStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Or;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new OrIdentityElementStrategy();
		yield return new OrIdempotencyStrategy();
		yield return new OrAllBitsSetStrategy();
		yield return new OrBooleanTrueStrategy();
		yield return new OrAbsorptionStrategy();
		yield return new OrCombineMasksStrategy();
		yield return new OrAndMaskAbsorptionStrategy();
	}
}
