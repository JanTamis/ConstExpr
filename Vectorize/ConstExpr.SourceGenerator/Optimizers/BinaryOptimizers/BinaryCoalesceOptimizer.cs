using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.CoalesceStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryCoalesceOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.None;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		// null ?? x => x
		yield return new CoalesceNullLeftStrategy();
		// literal ?? x => literal (literal is provably non-null)
		yield return new CoalesceNonNullLiteralLeftStrategy();
		// x ?? null => x
		yield return new CoalesceNullRightStrategy();
	}
}