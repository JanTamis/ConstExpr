using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class ConditionalPatternCombinerStrategy(BinaryOperatorKind operatorKind) : BaseBinaryStrategy<IsPatternExpressionSyntax, BinaryExpressionSyntax>
{
	public override bool TryOptimize(BinaryOptimizeContext<IsPatternExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		var patternKind = GetRelationalPatternKind();

		if (LeftEqualsRight(context.Left.Syntax.Expression, context.Right.Syntax.Left, context.Variables))
		{
			var rightPattern = ConvertToPattern(context.Right.Syntax.OperatorToken.Kind(), context.Right.Syntax.Right);

			if (rightPattern == null)
			{
				optimized = null;
				return false;
			}

			var combinedPattern = SyntaxFactory.BinaryPattern(
				patternKind,
				context.Left.Syntax.Pattern,
				rightPattern);

			optimized = SyntaxFactory.IsPatternExpression(context.Left.Syntax.Expression, combinedPattern);
			return true;
		}

		if (context.Left.Syntax.Expression.IsEquivalentTo(context.Right.Syntax.Right))
		{
			var rightPattern = ConvertToPattern(SwapCondition(context.Right.Syntax.OperatorToken.Kind()), context.Right.Syntax.Left);

			if (rightPattern == null)
			{
				optimized = null;
				return false;
			}

			var combinedPattern = SyntaxFactory.BinaryPattern(
				patternKind,
				context.Left.Syntax.Pattern,
				rightPattern);

			optimized = SyntaxFactory.IsPatternExpression(context.Left.Syntax.Expression, combinedPattern);
			return true;
		}

		optimized = null;
		return false;
	}

	private SyntaxKind GetRelationalPatternKind()
	{
		return operatorKind switch
		{
			BinaryOperatorKind.ConditionalAnd => SyntaxKind.AndPattern,
			BinaryOperatorKind.ConditionalOr => SyntaxKind.OrPattern,
			_ => SyntaxKind.None
		};
	}
}