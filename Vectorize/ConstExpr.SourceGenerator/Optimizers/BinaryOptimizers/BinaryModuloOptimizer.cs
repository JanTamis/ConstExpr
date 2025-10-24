using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryModuloOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Remainder;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new ModuloByOneStrategy();
		yield return new ModuloZeroStrategy();
		yield return new ModuloByNegativeOneStrategy();
		yield return new ModuloNormalizeNegativeDivisorStrategy();
		yield return new ModuloIdempotencyStrategy();
		yield return new ModuloNestedSimplificationStrategy();
		yield return new ModuloAlreadyMaskedStrategy();
		yield return new ModuloByPowerOfTwoStrategy();
	}
}