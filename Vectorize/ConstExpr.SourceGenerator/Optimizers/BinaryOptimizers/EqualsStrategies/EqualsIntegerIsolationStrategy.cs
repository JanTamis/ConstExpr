using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   v + c == k, v - c == k, c - v == k, v * c == k (odd c only) — see EqualityIntegerIsolationStrategy.
/// </summary>
public class EqualsIntegerIsolationStrategy : EqualityIntegerIsolationStrategy
{
	public EqualsIntegerIsolationStrategy() : base(SyntaxKind.EqualsExpression)
	{
	}
}