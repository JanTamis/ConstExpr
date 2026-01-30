using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalPatternStrategy(BinaryOperatorKind operatorKind) : BaseBinaryStrategy<BinaryExpressionSyntax, BinaryExpressionSyntax>
{
	// TODO: add support for left literal and right pure expression
	public override bool TryOptimize(BinaryOptimizeContext<BinaryExpressionSyntax, BinaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!LeftEqualsRight(context.Left.Syntax.Left, context.Right.Syntax.Left, context.Variables)
		    || !IsPure(context.Left.Syntax.Left)
		    || !IsPure(context.Right.Syntax.Left)
		    || context.Left.Syntax.Right is not LiteralExpressionSyntax
		    || context.Right.Syntax.Right is not LiteralExpressionSyntax)
		{
			optimized = null;
			return false;
		}
		
		var leftPattern = ConvertToPattern(context.Left.Syntax.OperatorToken.Kind(), context.Left.Syntax.Right);
		var rightPattern = ConvertToPattern(context.Right.Syntax.OperatorToken.Kind(), context.Right.Syntax.Right);
		
		if (leftPattern == null || rightPattern == null)
		{
			optimized = null;
			return false;
		}
		
		var andPattern = SyntaxFactory.BinaryPattern(GetRelationalPatternKind(), leftPattern, rightPattern);
		
		optimized = SyntaxFactory.IsPatternExpression(context.Left.Syntax.Left, andPattern);
		return true;
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