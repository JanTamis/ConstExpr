using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   c / v != k  =>  v != c / k — see EqualityReciprocalIsolationStrategy.
/// </summary>
public class NotEqualsReciprocalIsolationStrategy : EqualityReciprocalIsolationStrategy
{
	public NotEqualsReciprocalIsolationStrategy() : base(SyntaxKind.NotEqualsExpression)
	{
	}
}