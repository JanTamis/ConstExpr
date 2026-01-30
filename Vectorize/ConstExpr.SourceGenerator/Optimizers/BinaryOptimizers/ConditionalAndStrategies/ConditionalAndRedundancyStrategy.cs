using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

/// <summary>
/// Strategy for redundancy elimination: (a && b) && a => a && b (already covered, pure)
/// </summary>
public class ConditionalAndRedundancyStrategy() : SymmetricStrategy<BooleanBinaryStrategy, ExpressionSyntax, BinaryExpressionSyntax>(rightKind: SyntaxKind.LogicalAndExpression)
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