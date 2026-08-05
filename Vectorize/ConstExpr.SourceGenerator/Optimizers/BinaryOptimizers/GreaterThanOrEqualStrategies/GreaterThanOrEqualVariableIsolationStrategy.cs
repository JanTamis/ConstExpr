using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;

/// <summary>
///   v * c &gt;= k  =>  v &gt;= k / c   (flips to v &lt;= k / c when c is negative). Also v + c, c + v,
///   v - c, and c - v (see RelationalVariableIsolationStrategy).
/// </summary>
public class GreaterThanOrEqualVariableIsolationStrategy : RelationalVariableIsolationStrategy
{
	public GreaterThanOrEqualVariableIsolationStrategy() : base(SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.LessThanOrEqualExpression)
	{
	}
}