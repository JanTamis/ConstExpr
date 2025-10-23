using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class SpecialTypeBinaryStrategy : IBinaryStrategy
{
	public abstract SyntaxNode? Optimize(BinaryOptimizeContext context);

	public abstract bool CanBeOptimized(BinaryOptimizeContext context);

	public abstract bool IsValidSpecialType(SpecialType specialType);

	bool IBinaryStrategy.CanBeOptimized(BinaryOptimizeContext context)
	{
		return IsValidSpecialType(context.Type.SpecialType) && CanBeOptimized(context);
	}

	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}

	protected static bool LeftEqualsRight(BinaryOptimizeContext context)
	{
		return context.Left.IsEquivalentTo(context.Right) ||
					 (context.Left is IdentifierNameSyntax leftIdentifier
						&& context.Right is IdentifierNameSyntax rightIdentifier
						&& context.LeftValue is ArgumentSyntax leftArgument
						&& context.RightValue is ArgumentSyntax rightArgument
						&& leftArgument.Expression.IsEquivalentTo(rightArgument.Expression));
	}
}
