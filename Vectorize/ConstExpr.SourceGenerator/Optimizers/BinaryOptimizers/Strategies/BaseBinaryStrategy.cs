using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class BaseBinaryStrategy<TLeft, TRight> : IBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	public abstract bool TryOptimize(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized);

	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax or LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}

	protected static bool LeftEqualsRight(BinaryOptimizeContext<TLeft, TRight> context)
	{
		return LeftEqualsRight(context.Left.Syntax, context.Right.Syntax, context.TryGetLiteral);
	}

	protected static bool LeftEqualsRight(SyntaxNode left, SyntaxNode right, TryGetLiteralDelegate tryGetLiteral)
	{
		return left.IsEquivalentTo(right)
		       || left is IdentifierNameSyntax leftIdentifier
		       && right is IdentifierNameSyntax rightIdentifier
		        && (leftIdentifier.Identifier.Text == rightIdentifier.Identifier.Text ||
		       variables.TryGetValue(leftIdentifier.Identifier.Text, out var leftVar)
		       && variables.TryGetValue(rightIdentifier.Identifier.Text, out var rightVar)
		       && leftVar.Value is ArgumentSyntax leftArgument
		       && rightVar.Value is ArgumentSyntax rightArgument
		       && leftArgument.Expression.IsEquivalentTo(rightArgument.Expression));
	}

	protected static SyntaxKind SwapCondition(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanEqualsToken,
			SyntaxKind.LessThanToken => SyntaxKind.GreaterThanToken,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanToken,
			SyntaxKind.LessThanEqualsToken => SyntaxKind.GreaterThanToken,
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanEqualsToken,
			SyntaxKind.GreaterThanToken => SyntaxKind.LessThanEqualsToken,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanToken,
			SyntaxKind.GreaterThanEqualsToken => SyntaxKind.LessThanToken,
			_ => kind
		};
	}

	protected static PatternSyntax? ConvertToPattern(SyntaxKind operatorKind, ExpressionSyntax expression)
	{
		return operatorKind switch
		{
			SyntaxKind.EqualsEqualsToken => SyntaxFactory.ConstantPattern(expression),
			SyntaxKind.ExclamationEqualsToken => SyntaxFactory.UnaryPattern(
				SyntaxFactory.Token(SyntaxKind.NotKeyword),
				SyntaxFactory.ConstantPattern(expression)),
			SyntaxKind.LessThanToken or
				SyntaxKind.LessThanEqualsToken or
				SyntaxKind.GreaterThanToken or
				SyntaxKind.GreaterThanEqualsToken =>
				SyntaxFactory.RelationalPattern(SyntaxFactory.Token(operatorKind), expression),
			_ => null
		};
	}

	/// <summary>
	/// Checks if an expression is simple enough to duplicate without performance penalty.
	/// Simple expressions include: identifiers, literals, and member access.
	/// Complex expressions include: binary operations, invocations, etc.
	/// </summary>
	protected static bool IsSimpleExpression(ExpressionSyntax expr)
	{
		return expr switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			MemberAccessExpressionSyntax => true,
			ParenthesizedExpressionSyntax paren => IsSimpleExpression(paren.Expression),
			_ => false
		};
	}

	protected static bool IsPostive(SyntaxNode node, IDictionary<string, VariableItem> variables)
	{
		return node switch
		{
			LiteralExpressionSyntax literal => literal.Token.Value.IsPositive(),
			ArgumentSyntax argument => IsPostive(argument.Expression, variables),
			IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue => variable.Value.IsPositive(),
			_ => false
		};
	}

	protected static bool IsNegative(SyntaxNode node, IDictionary<string, VariableItem> variables)
	{
		return node switch
		{
			LiteralExpressionSyntax literal => literal.Token.Value.IsNegative(),
			ArgumentSyntax argument => IsNegative(argument.Expression, variables),
			IdentifierNameSyntax identifier when variables.TryGetValue(identifier.Identifier.Text, out var variable) && variable.HasValue => variable.Value.IsNegative(),
			_ => false
		};
	}
}