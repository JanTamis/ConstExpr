using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts <c>is</c> type-check + cast patterns to the modern
/// <c>is T name</c> pattern matching syntax.
/// Inspired by the Roslyn <c>CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer</c>.
///
/// Pattern 1 — if-statement with cast:
/// <code>
/// if (obj is MyType)
/// {
///     var x = (MyType)obj;
///     x.DoSomething();
/// }
/// </code>
/// →
/// <code>
/// if (obj is MyType x)
/// {
///     x.DoSomething();
/// }
/// </code>
///
/// Pattern 2 — as-cast with null check:
/// <code>
/// var x = obj as MyType;
/// if (x != null)
/// {
///     x.DoSomething();
/// }
/// </code>
/// →
/// <code>
/// if (obj is MyType x)
/// {
///     x.DoSomething();
/// }
/// </code>
/// </summary>
public static class UsePatternMatchingRefactoring
{
	/// <summary>
	/// Converts an if-statement with a type-check condition and an inner cast assignment
	/// to use pattern matching instead.
	///
	/// Before: <c>if (obj is MyType) { var x = (MyType)obj; ... }</c>
	/// After:  <c>if (obj is MyType x) { ... }</c>
	/// </summary>
	public static bool TryConvertIsAndCastToPattern(
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Condition must be: expr is Type
		if (ifStatement.Condition is not BinaryExpressionSyntax { Left: var checkedExpr, Right: TypeSyntax type } isExpr
		    || !isExpr.IsKind(SyntaxKind.IsExpression))
		{
			return false;
		}

		// Body must start with a cast assignment: var x = (Type)expr;

		if (ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 } body)
		{
			return false;
		}

		if (body.Statements[0] is not LocalDeclarationStatementSyntax { Declaration.Variables: [ var variable ] })
		{
			return false;
		}

		if (variable.Initializer?.Value is not CastExpressionSyntax cast)
		{
			return false;
		}

		// The cast type and checked type must match, and the cast target must match
		if (cast.Type.GetDeterministicHash() != type.GetDeterministicHash())
		{
			return false;
		}

		if (checkedExpr.GetDeterministicHash() != cast.Expression.GetDeterministicHash())
		{
			return false;
		}

		var variableName = variable.Identifier;

		// Build pattern: obj is MyType x
		var pattern = IsPatternExpression(
			checkedExpr,
			DeclarationPattern(
				type.WithoutTrivia(),
				SingleVariableDesignation(variableName)));

		// Remove the cast assignment from the body
		var newBody = body.WithStatements(body.Statements.RemoveAt(0));

		result = ifStatement
			.WithCondition(pattern)
			.WithStatement(newBody);

		return true;
	}

	/// <summary>
	/// Converts an as-cast followed by a null check into pattern matching.
	///
	/// Before:
	/// <code>
	/// var x = obj as MyType;
	/// if (x != null) { ... }
	/// </code>
	/// After:
	/// <code>
	/// if (obj is MyType x) { ... }
	/// </code>
	///
	/// Returns the replacement if-statement. The caller must remove the preceding
	/// local declaration from the containing block.
	/// </summary>
	public static bool TryConvertAsAndNullCheckToPattern(
		LocalDeclarationStatementSyntax localDecl,
		IfStatementSyntax ifStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		// Local must be: var x = expr as Type;
		if (localDecl.Declaration.Variables.Count != 1)
		{
			return false;
		}

		var variable = localDecl.Declaration.Variables[0];

		if (variable.Initializer?.Value is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AsExpression } asExpr)
		{
			return false;
		}

		if (asExpr.Right is not TypeSyntax type)
		{
			return false;
		}

		var variableName = variable.Identifier.ValueText;
		var sourceExpr = asExpr.Left;

		// if condition must be: x != null  or  x is not null
		if (!IsNullCheckOnVariable(ifStatement.Condition, variableName))
		{
			return false;
		}

		var pattern = IsPatternExpression(
			sourceExpr.WithoutTrivia(),
			DeclarationPattern(
				type.WithoutTrivia(),
				SingleVariableDesignation(variable.Identifier)));

		result = ifStatement.WithCondition(pattern);
		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the expression is a null check on a specific variable:
	/// <c>x != null</c> or <c>x is not null</c>.
	/// </summary>
	private static bool IsNullCheckOnVariable(ExpressionSyntax condition, string variableName)
	{
		switch (condition)
		{
			// x != null
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.NotEqualsExpression, Left: IdentifierNameSyntax leftId } binary
				when leftId.Identifier.ValueText == variableName
				     && binary.Right is LiteralExpressionSyntax rightLit
				     && rightLit.IsKind(SyntaxKind.NullLiteralExpression):
				return true;
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.NotEqualsExpression, Right: IdentifierNameSyntax rightId } binary
				when rightId.Identifier.ValueText == variableName
				     && binary.Left is LiteralExpressionSyntax leftLit
				     && leftLit.IsKind(SyntaxKind.NullLiteralExpression):
			// x is not null
			case IsPatternExpressionSyntax { Expression: IdentifierNameSyntax patternId } isPattern
				when patternId.Identifier.ValueText == variableName
				     && isPattern.Pattern is UnaryPatternSyntax notPattern
				     && notPattern.IsKind(SyntaxKind.NotPattern)
				     && notPattern.Pattern is ConstantPatternSyntax constPattern
				     && constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression):
				return true;
			default:
				return false;
		}

	}
}