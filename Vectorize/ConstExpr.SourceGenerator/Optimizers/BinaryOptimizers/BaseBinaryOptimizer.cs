using System.Collections.Generic;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer
{
	public abstract BinaryOperatorKind Kind { get; }

	public abstract IEnumerable<IBinaryStrategy> GetStrategies();
}