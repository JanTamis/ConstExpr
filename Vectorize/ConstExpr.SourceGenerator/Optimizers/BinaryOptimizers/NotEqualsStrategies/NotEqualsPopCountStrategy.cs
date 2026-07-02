using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.NotEqualsStrategies;

/// <summary>
///   Mirror of <see cref="EqualsPopCountStrategy" /> for <c>!=</c>:
///   <c>PopCount(x) != 0</c> => <c>x != 0</c> and <c>PopCount(x) != 1</c> => <c>!IsPow2(x)</c>.
/// </summary>
public class NotEqualsPopCountStrategy : EqualsPopCountStrategy
{
	protected override ExpressionSyntax CreateZeroComparison(ExpressionSyntax operand)
	{
		return NotEqualsExpression(operand, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
	}

	protected override ExpressionSyntax CreatePow2Result(ExpressionSyntax isPow2)
	{
		return LogicalNotExpression(isPow2);
	}
}