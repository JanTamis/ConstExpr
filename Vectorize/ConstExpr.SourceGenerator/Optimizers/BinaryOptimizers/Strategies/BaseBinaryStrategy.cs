using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class BaseBinaryStrategy : IBinaryStrategy
{
	public abstract bool CanBeOptimized(BinaryOptimizeContext context);
	public abstract SyntaxNode? Optimize(BinaryOptimizeContext context);
	
	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}

	protected static bool LeftEqualsRight(BinaryOptimizeContext context)
	{
		return context.Left.Syntax.IsEquivalentTo(context.Right.Syntax) ||
		       (context.Left.Syntax is IdentifierNameSyntax leftIdentifier
		        && context.Right.Syntax is IdentifierNameSyntax rightIdentifier
		        && context.Left.Value is ArgumentSyntax leftArgument
		        && context.Right.Value is ArgumentSyntax rightArgument
		        && leftArgument.Expression.IsEquivalentTo(rightArgument.Expression));
	}
}