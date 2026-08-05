using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;

/// <summary>
///   v * c &lt; k  =>  v &lt; k / c   (flips to v &gt; k / c when c is negative). Also v + c, c + v,
///   v - c, and c - v (see RelationalVariableIsolationStrategy).
/// </summary>
public class LessThanVariableIsolationStrategy : RelationalVariableIsolationStrategy
{
	public LessThanVariableIsolationStrategy() : base(SyntaxKind.LessThanExpression, SyntaxKind.GreaterThanExpression)
	{
	}
}