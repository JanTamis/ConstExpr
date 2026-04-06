using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that replaces a ternary conditional expression used in certain statement
/// contexts with an equivalent if/else statement.
/// Inspired by the Roslyn <c>ReplaceConditionalWithStatementsCodeRefactoringProvider</c>.
///
/// Supported patterns:
/// <list type="bullet">
///   <item><c>return cond ? a : b;</c>  →  <c>if (cond) return a; else return b;</c></item>
///   <item><c>x = cond ? a : b;</c>  →  <c>if (cond) x = a; else x = b;</c></item>
///   <item><c>yield return cond ? a : b;</c>  →  <c>if (cond) yield return a; else yield return b;</c></item>
///   <item><c>throw cond ? a : b;</c>  →  <c>if (cond) throw a; else throw b;</c></item>
/// </list>
/// </summary>
public static class ReplaceConditionalWithIfElseRefactoring
{
	/// <summary>
	/// Tries to replace a statement containing a conditional expression with an if/else.
	/// </summary>
	public static bool TryReplaceConditionalWithIfElse(
		StatementSyntax statement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		switch (statement)
		{
			// return cond ? a : b;
			case ReturnStatementSyntax { Expression: ConditionalExpressionSyntax cond }:
			{
				result = BuildIfElse(
					cond,
					ReturnStatement(cond.WhenTrue),
					ReturnStatement(cond.WhenFalse));
				return true;
			}

			// expression-statement: x = cond ? a : b;
			case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Right: ConditionalExpressionSyntax cond } assignment }:
			{
				result = BuildIfElse(
					cond,
					ExpressionStatement(assignment.WithRight(cond.WhenTrue)),
					
					ExpressionStatement(assignment.WithRight(cond.WhenFalse)));
				return true;
			}

			// yield return cond ? a : b;
			case YieldStatementSyntax
			{
				Expression: ConditionalExpressionSyntax cond
			} yieldStmt when yieldStmt.IsKind(SyntaxKind.YieldReturnStatement):
			{
				result = BuildIfElse(
					cond,
					YieldStatement(SyntaxKind.YieldReturnStatement, cond.WhenTrue),
					YieldStatement(SyntaxKind.YieldReturnStatement, cond.WhenFalse));
				return true;
			}

			// throw cond ? a : b;
			case ThrowStatementSyntax { Expression: ConditionalExpressionSyntax cond }:
			{
				result = BuildIfElse(
					cond,
					ThrowStatement(cond.WhenTrue),
					ThrowStatement(cond.WhenFalse));
				return true;
			}

			default:
			{
				return false;
			}
		}
	}

	private static IfStatementSyntax BuildIfElse(
		ConditionalExpressionSyntax cond,
		StatementSyntax whenTrue,
		StatementSyntax whenFalse)
	{
		return IfStatement(
			cond.Condition.WithoutTrivia(),
			Block(whenTrue),
			ElseClause(Block(whenFalse)));
	}
}

