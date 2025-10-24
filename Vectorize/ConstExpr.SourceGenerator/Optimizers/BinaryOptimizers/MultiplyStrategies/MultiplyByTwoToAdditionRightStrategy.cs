using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to addition: x * 2 => x + x (pure, non-integer)
/// </summary>
public class MultiplyByTwoToAdditionRightStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && !context.Type.IsInteger()
		       && context.Right.HasValue 
		       && context.Right.Value.IsNumericValue(2)
		       && IsPure(context.Left.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, context.Left.Syntax, context.Left.Syntax));
	}
}
