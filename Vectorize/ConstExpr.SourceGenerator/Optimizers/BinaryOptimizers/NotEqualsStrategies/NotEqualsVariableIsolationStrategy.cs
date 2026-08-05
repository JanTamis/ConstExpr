using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   v * c != k  =>  v != k / c   (also v + c, c + v, v - c, c - v). Same no-flip reasoning as
///   <see cref="EqualsStrategies.EqualsVariableIsolationStrategy" />.
/// </summary>
public class NotEqualsVariableIsolationStrategy : RelationalVariableIsolationStrategy
{
	public NotEqualsVariableIsolationStrategy() : base(SyntaxKind.NotEqualsExpression, SyntaxKind.NotEqualsExpression)
	{
	}
}