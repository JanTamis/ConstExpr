using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   Mirror of <see cref="EqualsRightShiftZeroStrategy" /> for <c>!=</c>:
///   <c>(x &gt;&gt; c) != 0</c> => <c>x &gt;= 2^c</c> (unsigned only). Safe under Strict.
/// </summary>
public class NotEqualsRightShiftZeroStrategy : EqualsRightShiftZeroStrategy
{
	protected override ExpressionSyntax CreateComparison(ExpressionSyntax operand, ExpressionSyntax boundary)
	{
		return GreaterThanOrEqualExpression(operand, boundary);
	}
}