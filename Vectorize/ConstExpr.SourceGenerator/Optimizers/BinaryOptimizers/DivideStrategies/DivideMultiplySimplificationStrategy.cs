using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.DivideStrategies;

/// <summary>
/// Strategy for algebraic simplification: (x * a) / a => x
/// </summary>
public class DivideMultiplySimplificationStrategy() : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.MultiplyExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Check if right side of multiply matches divisor
		if (LeftEqualsRight(context.Left.Syntax.Right, context.Right.Syntax, context.Variables)
		    && IsPure(context.Left.Syntax.Left))
		{
			optimized = context.Left.Syntax.Left;
			return true;
		}

		// Check if left side of multiply matches divisor
		if (LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax, context.Variables)
		    && IsPure(context.Left.Syntax.Right))
		{
			optimized = context.Left.Syntax.Right;
			return true;
		}
		
		optimized = null;
		return false;
	}
}
