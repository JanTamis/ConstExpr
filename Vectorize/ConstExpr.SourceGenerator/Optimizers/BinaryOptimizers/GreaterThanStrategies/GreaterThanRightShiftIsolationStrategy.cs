using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;

/// <summary>
///   v &gt;&gt; c &gt; k  =>  v &gt;= (k + 1) * 2^c (see RelationalRightShiftIsolationStrategy — floor
///   division makes <c>&gt;</c> sharpen to a non-strict <c>&gt;=</c> once k is bumped by one).
/// </summary>
public class GreaterThanRightShiftIsolationStrategy : RelationalRightShiftIsolationStrategy
{
	public GreaterThanRightShiftIsolationStrategy() : base(SyntaxKind.GreaterThanOrEqualExpression, true)
	{
	}
}