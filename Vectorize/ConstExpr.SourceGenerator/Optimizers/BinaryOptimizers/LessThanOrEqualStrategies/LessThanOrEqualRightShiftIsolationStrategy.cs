using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
///   v &gt;&gt; c &lt;= k  =>  v &lt; (k + 1) * 2^c (see RelationalRightShiftIsolationStrategy — floor
///   division makes <c>&lt;=</c> sharpen to a strict <c>&lt;</c> once k is bumped by one).
/// </summary>
public class LessThanOrEqualRightShiftIsolationStrategy : RelationalRightShiftIsolationStrategy
{
	public LessThanOrEqualRightShiftIsolationStrategy() : base(SyntaxKind.LessThanExpression, true)
	{
	}
}