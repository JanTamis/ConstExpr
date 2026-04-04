using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that inverts an if-statement by negating its condition and swapping
/// the true/false branches.
/// Inspired by the Roslyn <c>InvertIfCodeRefactoringProvider</c>.
///
/// <code>
/// if (condition) { A(); } else { B(); }
/// </code>
/// →
/// <code>
/// if (!condition) { B(); } else { A(); }
/// </code>
///
/// When there is no else clause but the if-body ends with a jump statement (return, break,
/// continue, throw), the code after the if is moved into the body and the original body
/// becomes the continuation.
/// </summary>
public static class InvertIfRefactoring
{
	/// <summary>
	/// Inverts an if-statement that has an else clause by negating the condition
	/// and swapping the true/false branches.
	/// </summary>
	public static bool TryInvertIf(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		if (ifStatement.Else is null)
		{
			return false;
		}

		var negatedCondition = NegateExpressionRefactoring.Negate(ifStatement.Condition);

		// Swap the branches
		var newTrueStatement = ifStatement.Else.Statement.WithTriviaFrom(ifStatement.Statement);
		var newFalseStatement = ifStatement.Statement.WithTriviaFrom(ifStatement.Else.Statement);

		// If the new false statement is itself an if-statement, wrap it in a block
		// to avoid dangling-else ambiguity.
		if (newTrueStatement is IfStatementSyntax)
		{
			newTrueStatement = Block(newTrueStatement);
		}

		result = ifStatement
			.WithCondition(negatedCondition)
			.WithStatement(newTrueStatement)
			.WithElse(ElseClause(newFalseStatement));

		return true;
	}

	/// <summary>
	/// Inverts an else-less if-statement that ends with a jump, pulling the subsequent
	/// code into the if-body and moving the original body after it.
	///
	/// Requires the if-statement to be directly inside a block.
	/// </summary>
	public static bool TryInvertIfWithoutElse(
		IfStatementSyntax ifStatement,
		BlockSyntax containingBlock,
		[NotNullWhen(true)] out BlockSyntax? result)
	{
		result = null;

		if (ifStatement.Else is not null)
		{
			return false;
		}

		// The body must end with a jump statement
		if (!EndsWithJump(ifStatement.Statement))
		{
			return false;
		}

		var stmtIndex = -1;
		var statements = containingBlock.Statements;

		for (var i = 0; i < statements.Count; i++)
		{
			if (statements[i] == ifStatement)
			{
				stmtIndex = i;
				break;
			}
		}

		if (stmtIndex < 0 || stmtIndex >= statements.Count - 1)
		{
			return false;
		}

		// Collect statements after the if
		var afterStatements = new SyntaxList<StatementSyntax>();

		for (var i = stmtIndex + 1; i < statements.Count; i++)
		{
			afterStatements = afterStatements.Add(statements[i]);
		}

		if (afterStatements.Count == 0)
		{
			return false;
		}

		// Build negated if with the "after" code as the new body
		var negatedCondition = NegateExpressionRefactoring.Negate(ifStatement.Condition);

		var newIf = IfStatement(
			negatedCondition,
			Block(afterStatements));

		// Build new block: everything before the if + new if + original if-body statements
		var newStatements = new SyntaxList<StatementSyntax>();

		for (var i = 0; i < stmtIndex; i++)
		{
			newStatements = newStatements.Add(statements[i]);
		}

		newStatements = newStatements.Add(newIf);

		// Add original if-body statements (unwrapped from block)
		foreach (var stmt in UnwrapBlock(ifStatement.Statement))
		{
			newStatements = newStatements.Add(stmt);
		}

		result = containingBlock.WithStatements(newStatements);
		return true;
	}

	private static bool EndsWithJump(StatementSyntax statement)
	{
		return statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && EndsWithJump(block.Statements.Last()),
			_ => false
		};
	}

	private static SyntaxList<StatementSyntax> UnwrapBlock(StatementSyntax statement)
	{
		return statement is BlockSyntax block
			? block.Statements
			: new SyntaxList<StatementSyntax>(statement);
	}
}

