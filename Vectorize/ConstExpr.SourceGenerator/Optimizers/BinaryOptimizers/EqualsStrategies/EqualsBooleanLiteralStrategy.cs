using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for boolean literal comparison: x == true => x, x == false => !x
/// </summary>
public class EqualsBooleanLiteralStrategy : BooleanBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
			&& context.Right is { HasValue: true, Value: bool };
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return context.Right.Value is true
				? context.Left.Syntax // x == true => x
				: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(context.Left.Syntax)); // x == false => !x
	}
}
