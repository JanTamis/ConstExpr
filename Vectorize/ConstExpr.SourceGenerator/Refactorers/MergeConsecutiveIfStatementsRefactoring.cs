using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that merges consecutive if-statements with identical bodies
/// into a single if-statement with a combined <c>||</c> condition.
/// Inspired by the Roslyn <c>MergeConsecutiveIfStatementsCodeRefactoringProvider</c>.
///
/// <code>
/// if (a) { Body(); }
/// if (b) { Body(); }
/// </code>
/// →
/// <code>
/// if (a || b) { Body(); }
/// </code>
///
/// Matching is based on syntax text equality of the statement bodies.
/// Both if-statements must lack else clauses.
/// </summary>
public static class MergeConsecutiveIfStatementsRefactoring
{
	/// <summary>
	/// Tries to merge two consecutive if-statements that have identical bodies.
	/// </summary>
	public static bool TryMergeConsecutiveIfs(
		IfStatementSyntax first,
		IfStatementSyntax second,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Neither may have else
		if (first.Else is not null || second.Else is not null)
		{
			return false;
		}

		// Bodies must be syntactically identical
		if (!AreStatementsEquivalent(first.Statement, second.Statement))
		{
			return false;
		}

		var combinedCondition = LogicalOrExpression(first.Condition, second.Condition);

		result = first.WithCondition(combinedCondition);
		return true;
	}

	/// <summary>
	/// Scans a block (or switch section) for consecutive if-statement pairs with identical
	/// bodies and merges them. Returns <see langword="true"/> if at least one merge occurred.
	/// </summary>
	public static bool TryMergeAllConsecutiveIfs(
		BlockSyntax block,
		[NotNullWhen(true)] out BlockSyntax? result)
	{
		result = null;
		var statements = block.Statements;
		var merged = new List<StatementSyntax>();
		var anyMerged = false;

		var i = 0;

		while (i < statements.Count)
		{
			if (i + 1 < statements.Count 
			    && statements[i] is IfStatementSyntax first 
			    && statements[i + 1] is IfStatementSyntax second 
			    && TryMergeConsecutiveIfs(first, second, out var mergedIf))
			{
				merged.Add(mergedIf);
				anyMerged = true;
				i += 2;
			}
			else
			{
				merged.Add(statements[i]);
				i++;
			}
		}

		if (!anyMerged)
		{
			return false;
		}

		result = block.WithStatements(List(merged));
		return true;
	}

	/// <summary>
	/// Compares two statements for syntactic equivalence (ignoring trivia).
	/// </summary>
	private static bool AreStatementsEquivalent(StatementSyntax a, StatementSyntax b)
	{
		return a.GetDeterministicHash() == b.GetDeterministicHash();
	}
}

