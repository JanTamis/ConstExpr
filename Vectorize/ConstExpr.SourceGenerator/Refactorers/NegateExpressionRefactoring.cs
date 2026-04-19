using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Provides expression negation utilities used by several refactorers.
/// Inspired by the Roslyn <c>NegateExpression</c> helpers found throughout the codebase.
///
/// Handles:
/// <list type="bullet">
///   <item>Double negation:          <c>!(!x)</c>  →  <c>x</c></item>
///   <item>Relational operators:     <c>a &lt; b</c>  →  <c>a &gt;= b</c></item>
///   <item>Equality operators:       <c>a == b</c>  →  <c>a != b</c></item>
///   <item>Logical operators:        <c>a &amp;&amp; b</c>  →  <c>!a || !b</c>  (De Morgan)</item>
///   <item><c>true</c> / <c>false</c> literals</item>
///   <item>Fallback:                 wraps with <c>!</c></item>
/// </list>
/// </summary>
public static class NegateExpressionRefactoring
{
	/// <summary>
	/// Tries to negate a boolean expression, producing a semantically inverted equivalent.
	/// Always succeeds (worst case wraps with <c>!</c>), so this overload exists for
	/// consistency with the other refactorers' Try-pattern but never returns <see langword="false"/>.
	/// </summary>
	public static bool TryNegate(
		ExpressionSyntax expression,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = Negate(expression);
		return true;
	}

	/// <summary>
	/// Returns the logical negation of <paramref name="expression"/>, simplified where possible.
	/// </summary>
	public static ExpressionSyntax Negate(ExpressionSyntax expression)
	{
		return expression switch
		{
			// !!x  →  x
			PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpr
				=> notExpr.Operand.WithTriviaFrom(expression),

			// true → false, false → true
			LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression)
				=> LiteralExpression(SyntaxKind.FalseLiteralExpression).WithTriviaFrom(expression),
			LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression)
				=> LiteralExpression(SyntaxKind.TrueLiteralExpression).WithTriviaFrom(expression),

			// Parenthesized: negate inside
			ParenthesizedExpressionSyntax paren
				=> ParenthesizedExpression(Negate(paren.Expression)).WithTriviaFrom(expression),

			// Relational & equality operators
			BinaryExpressionSyntax binary when InvertLogicalRefactoring.TryInvertLogical(binary, out var inverted)
				=> inverted.WithTriviaFrom(expression),
			
			LiteralExpressionSyntax or IdentifierNameSyntax or ElementAccessExpressionSyntax => expression,

			// Fallback: !expr
			_ => LogicalNotExpression(ParenthesizeIfNeeded(expression)).WithTriviaFrom(expression)
		};
	}

	/// <summary>
	/// Wraps the expression in parentheses if it is a binary or conditional expression,
	/// to preserve correct precedence when prepending <c>!</c>.
	/// </summary>
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
	{
		return expression switch
		{
			BinaryExpressionSyntax or ConditionalExpressionSyntax or AssignmentExpressionSyntax
				=> ParenthesizedExpression(expression),
			_ => expression
		};
	}
}

