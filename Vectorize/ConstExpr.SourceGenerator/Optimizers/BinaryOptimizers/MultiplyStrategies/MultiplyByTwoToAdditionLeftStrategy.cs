using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;

/// <summary>
/// Strategy for multiplication by two to addition: 2 * x => x + x (pure, non-integer)
/// </summary>
public class MultiplyByTwoToAdditionLeftStrategy : NumericBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return base.CanBeOptimized(context) 
		       && !context.Type.IsInteger()
		       && context.Left.HasValue 
		       && context.Left.Value.IsNumericValue(2)
		       && IsPure(context.Right.Syntax);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		return ParenthesizedExpression(BinaryExpression(SyntaxKind.AddExpression, context.Right.Syntax, context.Right.Syntax));
	}
}
