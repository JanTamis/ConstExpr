using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.SubtractStrategies;

/// <summary>
/// Strategy for algebraic identity: (x + a) - a => x (pure)
/// </summary>
public class SubtractAdditionCancellationRightStrategy() : NumericBinaryStrategy<BinaryExpressionSyntax, ExpressionSyntax>(leftKind: SyntaxKind.AddExpression)
{
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!base.TryOptimize(context, out optimized)
		    || !LeftEqualsRight(context.Left.Syntax.Right, context.Right.Syntax, context.Variables)
		    || !IsPure(context.Left.Syntax)
		    || !IsPure(context.Right.Syntax))
			return false;
		
		optimized = context.Left.Syntax.Left;
		return true;
	}
}
