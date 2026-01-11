using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalOrOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalOr;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ConditionalOrLiteralStrategy();
		yield return new ConditionalOrIdempotencyStrategy();
		yield return new ConditionalOrAbsorptionStrategy();
		yield return new ConditionalOrAbsorptionOrStrategy();
		yield return new ConditionalOrTautologyStrategy();
		yield return new ConditionalOrDeMorganStrategy();
		yield return new ConditionalOrIsNullOrEmptyStrategy();

		yield return new ConditionalPatternStrategy(Kind);
		yield return new ConditionalPatternCombinerStrategy(Kind);
		yield return new ConditionalPatternCombinersStrategy(Kind);
	}
}