using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that splits a compound if-statement condition into nested if-statements.
/// Inspired by the Roslyn <c>SplitIntoNestedIfStatementsCodeRefactoringProvider</c>.
///
/// <code>
/// if (a &amp;&amp; b)
/// {
///     Body();
/// }
/// </code>
/// →
/// <code>
/// if (a)
/// {
///     if (b)
///     {
///         Body();
///     }
/// }
/// </code>
///
/// Only splits on the top-level <c>&amp;&amp;</c> operator. The if must not have an else clause.
/// </summary>
public static class SplitIfStatementConditionRefactoring
{
	/// <summary>
	/// Splits an if-statement with a <c>&amp;&amp;</c> condition into nested if-statements.
	/// </summary>
	public static bool TrySplitIntoNestedIf(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Must not have else
		if (ifStatement.Else is not null)
		{
			return false;
		}

		// Condition must be &&
		if (ifStatement.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } binaryCondition)
		{
			return false;
		}

		var innerIf = IfStatement(
			binaryCondition.Right.WithoutTrivia(),
			ifStatement.Statement);

		result = ifStatement
			.WithCondition(binaryCondition.Left.WithoutTrivia())
			.WithStatement(Block(innerIf));

		return true;
	}

	/// <summary>
	/// Splits an if-statement with a <c>||</c> condition into consecutive if-statements
	/// (each with the same body).
	/// Inspired by the Roslyn <c>SplitIntoConsecutiveIfStatementsCodeRefactoringProvider</c>.
	///
	/// <code>
	/// if (a || b) { Body(); }
	/// </code>
	/// →
	/// <code>
	/// if (a) { Body(); }
	/// if (b) { Body(); }
	/// </code>
	/// </summary>
	public static bool TrySplitIntoConsecutiveIfs(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out SyntaxList<StatementSyntax>? result)
	{
		result = null;

		// Must not have else
		if (ifStatement.Else is not null)
		{
			return false;
		}

		// Condition must be ||
		if (ifStatement.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } binaryCondition)
		{
			return false;
		}

		var first = ifStatement
			.WithCondition(binaryCondition.Left.WithoutTrivia());

		var second = ifStatement
			.WithCondition(binaryCondition.Right.WithoutTrivia());

		result = List([ (StatementSyntax)first, second ]);
		return true;
	}
}

