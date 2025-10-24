using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for division by negative one: x / -1 = -x
/// </summary>
public class DivideByNegativeOneStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && context.Right is { HasValue: true, Value: { } value } 
		       && value.IsNumericNegativeOne();
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, context.Left.Syntax);
	}
}
