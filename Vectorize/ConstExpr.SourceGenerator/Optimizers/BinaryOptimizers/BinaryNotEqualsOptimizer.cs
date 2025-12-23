using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryNotEqualsOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.NotEquals;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new NotEqualsReflexiveStrategy();
		yield return new NotEqualsModuloOddStrategy();
		yield return new NotEqualsModuloEvenStrategy();
		yield return new NotEqualsBitwiseAndOddStrategy();
		yield return new NotEqualsBitwiseAndEvenStrategy();
	}
}
