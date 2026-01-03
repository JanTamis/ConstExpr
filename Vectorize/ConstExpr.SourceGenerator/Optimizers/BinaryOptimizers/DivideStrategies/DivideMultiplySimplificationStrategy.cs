using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for algebraic simplification: (x * a) / a => x
/// </summary>
public class DivideMultiplySimplificationStrategy : BaseBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!context.Left.Syntax.IsKind(SyntaxKind.MultiplyExpression))
		{
			optimized = null;
			return false;
		}

		// Check if right side of multiply matches divisor
		if (context.Left.Syntax.Right.IsEquivalentTo(context.Right.Syntax)
		    && IsPure(context.Left.Syntax.Left))
		{
			optimized = context.Left.Syntax.Left;
			return true;
		}

		// Check if left side of multiply matches divisor
		if (context.Left.Syntax.Left.IsEquivalentTo(context.Right.Syntax)
		    && IsPure(context.Left.Syntax.Right))
		{
			optimized = context.Left.Syntax.Right;
			return true;
		}
		
		optimized = null;
		return false;
	}
}
