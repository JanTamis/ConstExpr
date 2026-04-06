using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts an if/else-if chain that returns or assigns values
/// into a conditional (ternary) expression chain.
/// Inspired by patterns found throughout Roslyn's code style analyzers.
///
/// <code>
/// if (a) return x;
/// else if (b) return y;
/// else return z;
/// </code>
/// →
/// <code>
/// return a ? x : b ? y : z;
/// </code>
///
/// Also handles assignment patterns:
/// <code>
/// if (a) result = x; else result = y;
/// </code>
/// →
/// <code>
/// result = a ? x : y;
/// </code>
/// </summary>
public static class ConvertIfElseToConditionalRefactoring
{
	/// <summary>
	/// Tries to convert an if/else return chain into a single return with a
	/// ternary conditional expression.
	/// </summary>
	public static bool TryConvertIfElseReturnToConditional(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out ReturnStatementSyntax? result)
	{
		result = null;

		if (!TryBuildConditionalFromReturnChain(ifStatement, out var conditional))
		{
			return false;
		}

		result = ReturnStatement(conditional);
		return true;
	}

	/// <summary>
	/// Tries to convert an if/else assignment chain into a single assignment
	/// with a ternary conditional expression.
	/// </summary>
	public static bool TryConvertIfElseAssignmentToConditional(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out ExpressionStatementSyntax? result)
	{
		result = null;

		if (ifStatement.Else is null)
		{
			return false;
		}

		if (!TryGetSingleAssignment(ifStatement.Statement, out var trueAssign) 
		    || !TryGetSingleAssignment(ifStatement.Else.Statement, out var falseAssign))
		{
			return false;
		}

		// Both must assign to the same target
		if (trueAssign.Left.GetDeterministicHash() != falseAssign.Left.GetDeterministicHash())
		{
			return false;
		}

		// Both must use the same assignment kind
		if (trueAssign.Kind() != falseAssign.Kind())
		{
			return false;
		}

		var conditional = ConditionalExpression(
			ifStatement.Condition,
			trueAssign.Right,
			falseAssign.Right);

		var assignment = AssignmentExpression(
			trueAssign.Kind(),
			trueAssign.Left,
			conditional);

		result = ExpressionStatement(assignment);
		return true;
	}

	// -----------------------------------------------------------------------
	// Private helpers
	// -----------------------------------------------------------------------

	private static bool TryBuildConditionalFromReturnChain(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (!TryGetSingleReturnExpression(ifStatement.Statement, out var whenTrue))
		{
			return false;
		}

		if (ifStatement.Else is null)
		{
			return false;
		}

		var elseStatement = ifStatement.Else.Statement;

		// Else is another if → recursive ternary
		if (elseStatement is IfStatementSyntax nestedIf)
		{
			if (!TryBuildConditionalFromReturnChain(nestedIf, out var nestedConditional))
			{
				return false;
			}

			result = ConditionalExpression(ifStatement.Condition, whenTrue, nestedConditional);
			return true;
		}

		// Else is a direct return
		if (!TryGetSingleReturnExpression(elseStatement, out var whenFalse))
		{
			return false;
		}

		result = ConditionalExpression(ifStatement.Condition, whenTrue, whenFalse);
		return true;
	}

	private static bool TryGetSingleReturnExpression(
		StatementSyntax statement,
		[NotNullWhen(true)] out ExpressionSyntax? expression)
	{
		expression = null;

		return statement switch
		{
			ReturnStatementSyntax { Expression: { } expr }
				=> (expression = expr) is not null,
			BlockSyntax { Statements: [ReturnStatementSyntax { Expression: { } expr }] }
				=> (expression = expr) is not null,
			_ => false
		};
	}

	private static bool TryGetSingleAssignment(
		StatementSyntax statement,
		[NotNullWhen(true)] out AssignmentExpressionSyntax? assignment)
	{
		assignment = null;

		var target = statement is BlockSyntax { Statements: [var single] }
			? single
			: statement;

		if (target is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign })
		{
			assignment = assign;
			return true;
		}

		return false;
	}
}

