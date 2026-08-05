using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;

/// <summary>
///   v &gt;&gt; c &gt;= k  =>  v &gt;= k * 2^c (see RelationalRightShiftIsolationStrategy).
/// </summary>
public class GreaterThanOrEqualRightShiftIsolationStrategy : RelationalRightShiftIsolationStrategy
{
	public GreaterThanOrEqualRightShiftIsolationStrategy() : base(SyntaxKind.GreaterThanOrEqualExpression, false)
	{
	}
}