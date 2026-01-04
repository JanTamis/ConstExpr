using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ModuloStrategies;

/// <summary>
/// Strategy for nested modulo simplification: (x % m) % n where m % n == 0 => x % n
/// </summary>
public class ModuloNestedSimplificationStrategy : IntegerBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsKind(SyntaxKind.ModuloExpression)
		    || !context.TryGetLiteral(context.Right.Syntax, out var rightValue)
		    || !context.TryGetLiteral(context.Left.Syntax.Right, out var innerRightValue)
		    || !innerRightValue.Modulo(rightValue).IsNumericZero())
			return false;
		
		optimized = BinaryExpression(SyntaxKind.ModuloExpression, context.Left.Syntax.Left, context.Right.Syntax);
		return true;
	}
}
