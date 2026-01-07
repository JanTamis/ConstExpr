using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.RightShiftStrategies;

/// <summary>
/// Strategy for combining shifts: ((x >> a) >> b) => x >> (a + b)
/// </summary>
public class RightShiftCombineStrategy : IntegerBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{ 
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsKind(SyntaxKind.RightShiftExpression)
		    || !context.TryGetValue(context.Right.Syntax, out var rightValue)
		    || !context.TryGetValue(context.Left.Syntax.Right, out var leftShiftValue)
		    || !SyntaxHelpers.TryGetLiteral(rightValue.Add(leftShiftValue), out var combinedLiteral))
			return false;
		
		optimized = BinaryExpression(SyntaxKind.RightShiftExpression, context.Left.Syntax.Left, combinedLiteral);
		return true;
	}
}
