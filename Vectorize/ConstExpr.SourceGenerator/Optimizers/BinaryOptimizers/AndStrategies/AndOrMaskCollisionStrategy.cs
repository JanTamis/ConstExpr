using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.AndStrategies;

/// <summary>
/// (x | mask) & mask => mask (when x is pure)
/// symmetric
/// </summary>
public class AndOrMaskCollisionStrategy() : SymmetricStrategy<NumericBinaryStrategy, BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.BitwiseOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!Equals(context.Left.Syntax.Left, context.Right.Syntax)
		    || !Equals(context.Left.Syntax.Right, context.Right.Syntax)
		    || !IsPure(context.Left.Syntax.Left)
		    || !IsPure(context.Right.Syntax))
		{
			optimized = null;
			return false;
		}
		
		optimized = context.Right.Syntax;
		return true;
	}
}
