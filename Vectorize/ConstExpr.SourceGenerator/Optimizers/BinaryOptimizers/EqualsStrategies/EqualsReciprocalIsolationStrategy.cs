using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   c / v == k  =>  v == c / k — see EqualityReciprocalIsolationStrategy.
/// </summary>
public class EqualsReciprocalIsolationStrategy : EqualityReciprocalIsolationStrategy
{
	public EqualsReciprocalIsolationStrategy() : base(SyntaxKind.EqualsExpression)
	{
	}
}