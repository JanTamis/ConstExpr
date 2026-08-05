using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;

/// <summary>
///   v &gt;&gt; c &lt; k  =>  v &lt; k * 2^c (see RelationalRightShiftIsolationStrategy).
/// </summary>
public class LessThanRightShiftIsolationStrategy : RelationalRightShiftIsolationStrategy
{
	public LessThanRightShiftIsolationStrategy() : base(SyntaxKind.LessThanExpression, false)
	{
	}
}