using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
///   v * c == k  =>  v == k / c   (also v + c, c + v, v - c, c - v). Equality has no direction to
///   flip, so both the unflipped and flipped kind passed to the shared base are EqualsExpression —
///   a negative multiply coefficient still changes the isolated threshold's sign, just never the
///   operator.
/// </summary>
public class EqualsVariableIsolationStrategy : RelationalVariableIsolationStrategy
{
	public EqualsVariableIsolationStrategy() : base(SyntaxKind.EqualsExpression, SyntaxKind.EqualsExpression)
	{
	}
}