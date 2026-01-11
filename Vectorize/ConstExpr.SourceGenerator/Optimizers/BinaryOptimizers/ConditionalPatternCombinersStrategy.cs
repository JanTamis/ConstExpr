using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class ConditionalPatternCombinersStrategy(BinaryOperatorKind operatorKind) : BaseBinaryStrategy<IsPatternExpressionSyntax, IsPatternExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<IsPatternExpressionSyntax, IsPatternExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax.Expression, context.Right.Syntax.Expression, context.Variables))
		{
			optimized = null;
			return false;
		}
		
		var combinedPattern = SyntaxFactory.BinaryPattern(
			GetRelationalPatternKind(operatorKind),
			context.Left.Syntax.Pattern,
			context.Right.Syntax.Pattern);
		
		optimized = SyntaxFactory.IsPatternExpression(context.Left.Syntax.Expression, combinedPattern);
		return true;
	}

	private SyntaxKind GetRelationalPatternKind(BinaryOperatorKind operatorKind)
	{
		return operatorKind switch
		{
			BinaryOperatorKind.ConditionalAnd => SyntaxKind.AndPattern,
			BinaryOperatorKind.ConditionalOr => SyntaxKind.OrPattern,
			_ => SyntaxKind.None
		};
	}
}