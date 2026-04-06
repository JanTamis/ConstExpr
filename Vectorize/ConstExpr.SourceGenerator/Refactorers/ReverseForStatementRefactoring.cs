using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that reverses the direction of a <c>for</c> loop.
/// Inspired by the Roslyn <c>CSharpReverseForStatementCodeRefactoringProvider</c>.
///
/// <code>
/// for (var i = start; i &lt; end; i++)
/// </code>
/// →
/// <code>
/// for (var i = end - 1; i &gt;= start; i--)
/// </code>
///
/// Supports:
/// <list type="bullet">
///   <item><c>i++</c>, <c>++i</c>, <c>i += 1</c>  ↔  <c>i--</c>, <c>--i</c>, <c>i -= 1</c></item>
///   <item><c>&lt;</c> / <c>&lt;=</c>  ↔  <c>&gt;=</c> / <c>&gt;</c> conditions</item>
/// </list>
///
/// This is a pure syntax-level refactoring; it does not check for unsigned boundary cases.
/// </summary>
public static class ReverseForStatementRefactoring
{
	/// <summary>
	/// Tries to reverse the direction of a for-loop.
	/// </summary>
	public static bool TryReverseForStatement(
		ForStatementSyntax forStatement,
		[NotNullWhen(true)] out ForStatementSyntax? result)
	{
		result = null;

		var declaration = forStatement.Declaration;

		if (declaration is null 
		    || declaration.Variables.Count != 1 
		    || forStatement.Incrementors.Count != 1)
		{
			return false;
		}

		var variable = declaration.Variables[0];
		var after = forStatement.Incrementors[0];

		if (forStatement.Condition is not BinaryExpressionSyntax condition)
		{
			return false;
		}

		if (MatchesIncrementPattern(variable, condition, after, out var start, out var equals, out var end))
		{
			// for (var x = start; x < end; x++)  →  for (var x = end - 1; x >= start; x--)
			// for (var x = start; x <= end; x++)  →  for (var x = end; x >= start; x--)
			var newInit = equals
				? end
				: SubtractExpression(end, CreateLiteral(1));

			var newCondition = GreaterThanOrEqualExpression(IdentifierName(variable.Identifier), start);

			var newAfter = InvertAfter(after);

			result = forStatement
				.WithDeclaration(declaration.WithVariables(
					SingletonSeparatedList(variable.WithInitializer(
						EqualsValueClause(newInit)))))
				.WithCondition(newCondition)
				.WithIncrementors(SingletonSeparatedList(newAfter));

			return true;
		}

		if (MatchesDecrementPattern(variable, condition, after, out end, out start))
		{
			// for (var x = end; x >= start; x--)  →  for (var x = start; x <= end; x++)
			var newCondition = LessThanOrEqualExpression(IdentifierName(variable.Identifier), end);
			var newAfter = InvertAfter(after);

			result = forStatement
				.WithDeclaration(declaration.WithVariables(
					SingletonSeparatedList(variable.WithInitializer(
						EqualsValueClause(start)))))
				.WithCondition(newCondition)
				.WithIncrementors(SingletonSeparatedList(newAfter));

			return true;
		}

		return false;
	}

	// -----------------------------------------------------------------------
	// Pattern matching helpers
	// -----------------------------------------------------------------------

	private static bool MatchesIncrementPattern(
		VariableDeclaratorSyntax variable, BinaryExpressionSyntax condition,
		ExpressionSyntax after,
		[NotNullWhen(true)] out ExpressionSyntax? start, out bool equals,
		[NotNullWhen(true)] out ExpressionSyntax? end)
	{
		start = variable.Initializer?.Value;
		equals = false;
		end = null;

		if (start is null)
		{
			return false;
		}

		// i < end  /  i <= end
		if (condition.IsKind(SyntaxKind.LessThanExpression, SyntaxKind.LessThanOrEqualExpression) 
		    && IsVariableReference(variable, condition.Left))
		{
			end = condition.Right;
			equals = condition.IsKind(SyntaxKind.LessThanOrEqualExpression);
		}
		// end > i  /  end >= i
		else if (condition.IsKind(SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanOrEqualExpression) 
		         && IsVariableReference(variable, condition.Right))
		{
			end = condition.Left;
			equals = condition.IsKind(SyntaxKind.GreaterThanOrEqualExpression);
		}

		return end is not null && IsIncrementAfter(variable, after);
	}

	private static bool MatchesDecrementPattern(
		VariableDeclaratorSyntax variable, BinaryExpressionSyntax condition,
		ExpressionSyntax after,
		[NotNullWhen(true)] out ExpressionSyntax? end,
		[NotNullWhen(true)] out ExpressionSyntax? start)
	{
		end = variable.Initializer?.Value;
		start = null;

		if (end is null)
		{
			return false;
		}

		// i >= start
		if (condition.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
		    && IsVariableReference(variable, condition.Left))
		{
			start = condition.Right;
		}
		// start <= i
		else if (condition.IsKind(SyntaxKind.LessThanOrEqualExpression) 
		         && IsVariableReference(variable, condition.Right))
		{
			start = condition.Left;
		}

		return start is not null && IsDecrementAfter(variable, after);
	}

	private static bool IsVariableReference(VariableDeclaratorSyntax variable, ExpressionSyntax expr)
	{
		return expr is IdentifierNameSyntax id 
		       && id.Identifier.ValueText == variable.Identifier.ValueText;
	}

	private static bool IsIncrementAfter(VariableDeclaratorSyntax variable, ExpressionSyntax after)
	{
		return after switch
		{
			PostfixUnaryExpressionSyntax post when post.IsKind(SyntaxKind.PostIncrementExpression)
				=> IsVariableReference(variable, post.Operand),
			PrefixUnaryExpressionSyntax pre when pre.IsKind(SyntaxKind.PreIncrementExpression)
				=> IsVariableReference(variable, pre.Operand),
			AssignmentExpressionSyntax assign when assign.IsKind(SyntaxKind.AddAssignmentExpression)
				=> IsVariableReference(variable, assign.Left) && IsLiteralOne(assign.Right),
			_ => false
		};
	}

	private static bool IsDecrementAfter(VariableDeclaratorSyntax variable, ExpressionSyntax after)
	{
		return after switch
		{
			PostfixUnaryExpressionSyntax post when post.IsKind(SyntaxKind.PostDecrementExpression)
				=> IsVariableReference(variable, post.Operand),
			PrefixUnaryExpressionSyntax pre when pre.IsKind(SyntaxKind.PreDecrementExpression)
				=> IsVariableReference(variable, pre.Operand),
			AssignmentExpressionSyntax assign when assign.IsKind(SyntaxKind.SubtractAssignmentExpression)
				=> IsVariableReference(variable, assign.Left) && IsLiteralOne(assign.Right),
			_ => false
		};
	}

	private static bool IsLiteralOne(ExpressionSyntax expr)
	{
		return expr is LiteralExpressionSyntax { Token.Value: 1 };
	}

	/// <summary>
	/// Inverts the incrementor: <c>++</c> ↔ <c>--</c>, <c>+=</c> ↔ <c>-=</c>.
	/// </summary>
	private static ExpressionSyntax InvertAfter(ExpressionSyntax after)
	{
		return after switch
		{
			PostfixUnaryExpressionSyntax post => post.WithOperatorToken(
				Token(post.OperatorToken.IsKind(SyntaxKind.PlusPlusToken)
					? SyntaxKind.MinusMinusToken
					: SyntaxKind.PlusPlusToken).WithTriviaFrom(post.OperatorToken)),

			PrefixUnaryExpressionSyntax pre => pre.WithOperatorToken(
				Token(pre.OperatorToken.IsKind(SyntaxKind.PlusPlusToken)
					? SyntaxKind.MinusMinusToken
					: SyntaxKind.PlusPlusToken).WithTriviaFrom(pre.OperatorToken)),

			AssignmentExpressionSyntax assign => assign.WithOperatorToken(
				Token(assign.OperatorToken.IsKind(SyntaxKind.PlusEqualsToken)
					? SyntaxKind.MinusEqualsToken
					: SyntaxKind.PlusEqualsToken).WithTriviaFrom(assign.OperatorToken)),

			_ => after
		};
	}
}



