using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAndOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.And;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		// Literal/identity-like strategies (x & 0, 0 & x, booleans true/false)
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndIdentityElementStrategy();
		// Idempotency: x & x = x (for pure expressions)
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndIdempotencyStrategy();
		// All-bits-set absorption: x & ~0 = x and ~0 & x = x
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndAllBitsSetStrategy();
		// Absorption with Or: x & (x | y) = x and (x | y) & x = x
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndAbsorptionStrategy();
		// Combine masks: (x & mask1) & mask2 => x & (mask1 & mask2)
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndCombineMasksStrategy();
		// (x | mask) & mask => mask (when x is pure)
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndOrMaskCollisionStrategy();
		// (x | mask1) & mask2 when mask1 & mask2 == 0 => x & mask2 (when x is pure)
		yield return new ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies.AndOrMaskIntersectionZeroStrategy();
	}
}
