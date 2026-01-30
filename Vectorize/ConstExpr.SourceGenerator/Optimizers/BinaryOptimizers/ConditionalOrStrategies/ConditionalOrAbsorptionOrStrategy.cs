using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalOrStrategies;

/// <summary>
/// Strategy for absorption law: a || (a || b) => a || b (pure) or (a || b) || a => a || b (pure)
/// </summary>
public class ConditionalOrAbsorptionOrStrategy() : SymmetricStrategy<BooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>(rightKind: SyntaxKind.LogicalOrExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<ExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsPure(context.Left.Syntax)
		    || !LeftEqualsRight(context.Right.Syntax.Left, context.Left.Syntax, context.Variables)
		    && !LeftEqualsRight(context.Right.Syntax.Right, context.Left.Syntax, context.Variables))
		{
			optimized = null;
			return false;
		}

		optimized = context.Right.Syntax;
		return true;
	}
}