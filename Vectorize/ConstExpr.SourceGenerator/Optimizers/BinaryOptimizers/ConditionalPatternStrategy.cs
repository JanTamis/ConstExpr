using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalPatternStrategy : BaseBinaryStrategy
{
	public override bool CanBeOptimized(BinaryOptimizeContext context)
	{
		return context.Left.Syntax is BinaryExpressionSyntax { Right: LiteralExpressionSyntax } left
			&& context.Right.Syntax is BinaryExpressionSyntax { Right: LiteralExpressionSyntax } right
			&& left.Left.IsEquivalentTo(right.Left)
			&& IsPure(left.Left)
			&& IsPure(right.Left);
	}

	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		if (context.Left.Syntax is not BinaryExpressionSyntax left
		    || context.Right.Syntax is not BinaryExpressionSyntax right)
		{
			return null;
		}

		var leftPattern = ConvertToPattern(left.OperatorToken.Kind(), left.Right);
		var rightPattern = ConvertToPattern(right.OperatorToken.Kind(), right.Right);
		
		if (leftPattern == null || rightPattern == null)
		{
			return null;
		}
		
		var andPattern = SyntaxFactory.BinaryPattern(GetRelationalPatternKind(context.Kind), leftPattern, rightPattern);
		
		return SyntaxFactory.IsPatternExpression(left.Left, andPattern);
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