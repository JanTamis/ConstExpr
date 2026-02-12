using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryConditionalAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.ConditionalAnd;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ConditionalAndLiteralStrategy();
		yield return new ConditionalAndRightLiteralStrategy();
		yield return new ConditionalAndAbsorptionStrategy();
		yield return new ConditionalAndRedundancyStrategy();
		yield return new ConditionalAndContradictionStrategy();
		yield return new ConditionalAndIdempotencyStrategy();
		yield return new ConditionalAndDeMorganStrategy();
		yield return new ConditionalAndCharOptimizer();
		
		yield return new ConditionalPatternStrategy(Kind);
		yield return new ConditionalPatternCombinerStrategy(Kind);
		yield return new ConditionalPatternCombinersStrategy(Kind);
	}
}

