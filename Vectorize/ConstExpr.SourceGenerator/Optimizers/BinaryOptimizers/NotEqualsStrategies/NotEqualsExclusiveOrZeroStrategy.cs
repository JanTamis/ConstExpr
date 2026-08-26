using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   Mirror of <see cref="EqualsExclusiveOrZeroStrategy" /> for !=:
///   (a ^ b) != 0 => a != b. Safe under Strict.
/// </summary>
public class NotEqualsExclusiveOrZeroStrategy : EqualsExclusiveOrZeroStrategy
{
	protected override ExpressionSyntax CreateComparison(ExpressionSyntax left, ExpressionSyntax right)
	{
		return NotEqualsExpression(left, right);
	}
}