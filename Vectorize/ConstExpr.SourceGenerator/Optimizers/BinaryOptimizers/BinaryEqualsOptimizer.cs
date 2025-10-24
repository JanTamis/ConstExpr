using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryEqualsOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Equals;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new EqualsIdempotencyStrategy();
		yield return new EqualsBooleanLiteralStrategy();
		yield return new EqualsModuloEvenStrategy();
		yield return new EqualsModuloOddStrategy();
		yield return new EqualsBitwiseAndEvenStrategy();
		yield return new EqualsBitwiseAndOddStrategy();
	}
}
