using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for algebraic identity: (a + x) - a => x (pure)
/// </summary>
public class SubtractAdditionCancellationLeftStrategy : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !context.Left.Syntax.IsKind(SyntaxKind.AddExpression)
		    || !LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax, context.TryGetLiteral)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
			return false;
		
		optimized = context.Left.Syntax.Right;
		return true;
	}
}
