using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that merges nested if-statements into a single if-statement by
/// combining conditions with <c>&amp;&amp;</c>.
/// Inspired by the Roslyn <c>MergeNestedIfStatementsCodeRefactoringProvider</c>.
///
/// <code>
/// if (a)
/// {
///     if (b)
///     {
///         Body();
///     }
/// }
/// </code>
/// →
/// <code>
/// if (a &amp;&amp; b)
/// {
///     Body();
/// }
/// </code>
/// </summary>
public static class MergeNestedIfStatementsRefactoring
{
	/// <summary>
	/// Tries to merge a nested if statement into its parent if statement.
	/// The outer if must contain only the inner if (no other statements),
	/// and neither may have an else clause.
	/// </summary>
	public static bool TryMergeNestedIf(
		IfStatementSyntax outerIf,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Outer must not have else
		if (outerIf.Else is not null)
		{
			return false;
		}

		// Get the single inner if
		if (!TryGetSingleNestedIf(outerIf.Statement, out var innerIf))
		{
			return false;
		}

		// Inner must not have else
		if (innerIf.Else is not null)
		{
			return false;
		}

		// Combine conditions with &&
		var combinedCondition = LogicalAndExpression(ParenthesizeIfNeeded(outerIf.Condition), ParenthesizeIfNeeded(innerIf.Condition));

		result = outerIf
			.WithCondition(combinedCondition)
			.WithStatement(innerIf.Statement);

		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the statement is (or wraps) a single
	/// <see cref="IfStatementSyntax"/> with no surrounding statements.
	/// </summary>
	private static bool TryGetSingleNestedIf(
		StatementSyntax statement,
		[NotNullWhen(true)] out IfStatementSyntax? innerIf)
	{
		innerIf = null;

		switch (statement)
		{
			case IfStatementSyntax directIf:
			{
				innerIf = directIf;
				return true;
			}

			case BlockSyntax { Statements: [ IfStatementSyntax blockIf ] }:
			{
				innerIf = blockIf;
				return true;
			}

			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Wraps binary or-expressions in parentheses to preserve precedence when combined with <c>&amp;&amp;</c>.
	/// </summary>
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expr)
	{
		if (expr is BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression })
		{
			return ParenthesizedExpression(expr);
		}
		
		return expr;
	}
}