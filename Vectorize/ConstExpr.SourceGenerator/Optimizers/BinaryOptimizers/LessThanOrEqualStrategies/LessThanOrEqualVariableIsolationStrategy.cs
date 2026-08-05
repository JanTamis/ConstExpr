using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
///   v * c &lt;= k  =>  v &lt;= k / c   (flips to v &gt;= k / c when c is negative). Also v + c, c + v,
///   v - c, and c - v (see RelationalVariableIsolationStrategy).
/// </summary>
public class LessThanOrEqualVariableIsolationStrategy : RelationalVariableIsolationStrategy
{
	public LessThanOrEqualVariableIsolationStrategy() : base(SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanOrEqualExpression)
	{
	}
}