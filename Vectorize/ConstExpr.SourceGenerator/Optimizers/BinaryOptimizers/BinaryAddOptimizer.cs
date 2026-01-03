using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AddStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryAddOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Add;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new AddConstantFoldingStrategy();
		yield return new AddDoubleNegatedStrategy();
		yield return new AddDoubleToShiftStrategy();
		yield return new AddFusedMultiplyAddStrategy();
		yield return new AddIdentityElementStrategy();
		yield return new AddNegatedSubtractionStrategy();
		yield return new AddNegationStrategy();
		yield return new AddSubtractionCancellationStrategy();
	}
}