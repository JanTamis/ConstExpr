using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public abstract class BaseBinaryStrategy<TLeft, TRight> : IBinaryStrategy<TLeft, TRight>
	where TLeft : ExpressionSyntax
	where TRight : ExpressionSyntax
{
	/// <summary>
	/// Gets the FastMathFlags required for this optimization strategy to be applied.
	/// Strategies that don't require fast-math flags should return <see cref="FastMathFlags.Strict"/>.
	/// Override this property in derived classes to specify required flags.
	/// </summary>
	public virtual FastMathFlags RequiredFlags => FastMathFlags.Strict;

	public abstract bool TryOptimize(BinaryOptimizeContext<TLeft, TRight> context, out ExpressionSyntax? optimized);

	public BinaryOptimizeContext<TLeft, TRight>? GetContext(
		List<BinaryExpressionSyntax> expressions, 
		ITypeSymbol type, 
		ExpressionSyntax leftExpr, 
		ITypeSymbol? leftType, 
		ExpressionSyntax rightExpr, 
		ITypeSymbol? rightType,
		IDictionary<string, VariableItem> variables,
		TryGetValueDelegate tryGetValue,
		SyntaxNode? parent)
	{
		if (leftExpr is not TLeft typedLeft || rightExpr is not TRight typedRight)
		{
			return null;
		}

		return new BinaryOptimizeContext<TLeft, TRight>
		{
			Left = new BinaryOptimizeElement<TLeft>
			{
				Type = leftType,
				Syntax = typedLeft,
			},
			Right = new BinaryOptimizeElement<TRight>
			{
				Type = rightType,
				Syntax = typedRight,
			},
			Type = type,
			Variables = variables,
			TryGetValue = tryGetValue,
			BinaryExpressions = expressions,
			Parent = parent,
		};
	}

	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax or LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			MemberAccessExpressionSyntax m => IsPure(m.Expression),
			_ => false
		};
	}

	protected static bool LeftEqualsRight(BinaryOptimizeContext<TLeft, TRight> context)
	{
		return LeftEqualsRight(context.Left.Syntax, context.Right.Syntax, context.Variables);
	}

	protected static bool LeftEqualsRight(SyntaxNode left, SyntaxNode right, IDictionary<string, VariableItem> variables)
	{
		return left.IsEquivalentTo(right)
		       || left is IdentifierNameSyntax leftIdentifier
		       && right is IdentifierNameSyntax rightIdentifier
		       && (leftIdentifier.Identifier.Text == rightIdentifier.Identifier.Text
		           || variables.TryGetValue(leftIdentifier.Identifier.Text, out var leftVar)
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
			SyntaxKind.EqualsEqualsToken => ConstantPattern(expression),
			SyntaxKind.ExclamationEqualsToken => UnaryPattern(
				Token(SyntaxKind.NotKeyword),
				ConstantPattern(expression)),
			SyntaxKind.LessThanToken or
				SyntaxKind.LessThanEqualsToken or
				SyntaxKind.GreaterThanToken or
				SyntaxKind.GreaterThanEqualsToken =>
				RelationalPattern(Token(operatorKind), expression),
			_ => null
		};
	}

	/// <summary>
	/// Tries to interpret an expression as a pattern over some target expression.
	/// Handles binary comparisons (<c>expr op lit</c> and <c>lit op expr</c>) and
	/// existing is-pattern expressions, so callers don't need separate branches per type.
	/// </summary>
	/// <returns>
	/// A <c>(Target, Pattern)</c> pair when the expression can be turned into a pattern,
	/// or <c>null</c> otherwise.
	/// </returns>
	protected static (ExpressionSyntax Target, PatternSyntax Pattern)? TryParseAsPattern(
		ExpressionSyntax expression)
	{
		switch (expression)
		{
			// Already a pattern — keep the expression and its pattern verbatim.
			case IsPatternExpressionSyntax { Expression: var expr } ip when IsPure(expr):
				return (expr, ip.Pattern);

			case BinaryExpressionSyntax b:
				// expr op literal  (e.g.  x > 0)
				if (CanBeUsedAsPattern(b.Right) && IsPure(b.Left))
				{
					var pattern = ConvertToPattern(b.OperatorToken.Kind(), b.Right);
					if (pattern is not null)
						return (b.Left, pattern);
				}

				// literal op expr  (e.g.  0 < x) — flip the operator so it reads as  x > 0
				if (CanBeUsedAsPattern(b.Left) && IsPure(b.Right))
				{
					var pattern = ConvertToPattern(SwapCondition(b.OperatorToken.Kind()), b.Left);
					if (pattern is not null)
						return (b.Right, pattern);
				}

				break;
		}

		return null;

		bool CanBeUsedAsPattern(ExpressionSyntax expr)
		{
			return expr is LiteralExpressionSyntax or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax };
		}
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

	protected bool IsPositive(BinaryOptimizeContext<TLeft, TRight> context, ExpressionSyntax node)
	{
		return context.BinaryExpressions.Any(a =>
		{
			return LeftEqualsRight(a.Left, node, context.Variables)
			       && a.OperatorToken.IsKind(SyntaxKind.GreaterThanToken, SyntaxKind.GreaterThanEqualsToken)
			       && IsPostive(a.Right, context.Variables)
			       || LeftEqualsRight(a.Right, node, context.Variables)
			       && a.OperatorToken.IsKind(SyntaxKind.LessThanToken, SyntaxKind.LessThanEqualsToken)
			       && IsNegative(a.Left, context.Variables);
		});
	}
}

public abstract class BaseBinaryStrategy : BaseBinaryStrategy<ExpressionSyntax, ExpressionSyntax>;