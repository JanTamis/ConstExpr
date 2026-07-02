using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryEqualsOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Equals;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ToLowerOptimizer();
		yield return new ToUpperOptimizer();
		yield return new EqualsToLowerStrategy();
		yield return new EqualsToUpperStrategy();
		yield return new EqualsIdempotencyStrategy();
		yield return new EqualsBooleanLiteralStrategy();
		yield return new EqualsCountZeroStrategy();
		yield return new EqualsModuloEvenStrategy();
		yield return new EqualsModuloOddStrategy();
		yield return new EqualsModuloPowerOfTwoZeroStrategy();
		yield return new EqualsBitwiseAndEvenStrategy();
		yield return new EqualsBitwiseAndOddStrategy();
		yield return new EqualsComparisonSimplifierStrategy();
		yield return new EqualsReverseStrategy();
	}
}