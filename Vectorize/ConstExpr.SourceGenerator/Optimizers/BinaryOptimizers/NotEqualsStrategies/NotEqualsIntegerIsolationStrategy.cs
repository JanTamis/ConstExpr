using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   v + c != k, v - c != k, c - v != k, v * c != k (odd c only) — see EqualityIntegerIsolationStrategy.
/// </summary>
public class NotEqualsIntegerIsolationStrategy : EqualityIntegerIsolationStrategy
{
	public NotEqualsIntegerIsolationStrategy() : base(SyntaxKind.NotEqualsExpression)
	{
	}
}