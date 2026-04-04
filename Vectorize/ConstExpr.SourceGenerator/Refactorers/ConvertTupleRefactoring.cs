using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between tuples and explicit struct/class declarations,
/// and between named and unnamed tuple elements.
/// Inspired by the Roslyn <c>ConvertTupleToStruct</c> and <c>NameTupleElement</c> features.
///
/// <list type="bullet">
///   <item>Add names to unnamed tuple elements:
///     <c>(int, string)</c> → <c>(int Id, string Name)</c></item>
///   <item>Remove names from named tuple elements:
///     <c>(int Id, string Name)</c> → <c>(int, string)</c></item>
/// </list>
/// </summary>
public static class ConvertTupleRefactoring
{
	/// <summary>
	/// Removes element names from a named tuple type, producing an unnamed tuple type.
	/// </summary>
	public static bool TryRemoveTupleElementNames(
		TupleTypeSyntax tupleType,
		[NotNullWhen(true)] out TupleTypeSyntax? result)
	{
		result = null;

		var hasNames = false;

		foreach (var element in tupleType.Elements)
		{
			if (element.Identifier.ValueText != "")
			{
				hasNames = true;
				break;
			}
		}

		if (!hasNames)
		{
			return false;
		}

		var newElements = new SeparatedSyntaxList<TupleElementSyntax>();

		foreach (var element in tupleType.Elements)
		{
			newElements = newElements.Add(
				TupleElement(element.Type));
		}

		result = TupleType(newElements).WithTriviaFrom(tupleType);
		return true;
	}

	/// <summary>
	/// Removes element names from a tuple expression.
	/// <c>(Id: 1, Name: "Bob")</c> → <c>(1, "Bob")</c>
	/// </summary>
	public static bool TryRemoveTupleExpressionNames(
		TupleExpressionSyntax tupleExpr,
		[NotNullWhen(true)] out TupleExpressionSyntax? result)
	{
		result = null;

		var hasNames = false;

		foreach (var arg in tupleExpr.Arguments)
		{
			if (arg.NameColon is not null)
			{
				hasNames = true;
				break;
			}
		}

		if (!hasNames)
		{
			return false;
		}

		var newArguments = new SeparatedSyntaxList<ArgumentSyntax>();

		foreach (var arg in tupleExpr.Arguments)
		{
			newArguments = newArguments.Add(Argument(arg.Expression).WithTriviaFrom(arg));
		}

		result = TupleExpression(newArguments).WithTriviaFrom(tupleExpr);
		return true;
	}

	/// <summary>
	/// Deconstructs a tuple assignment into separate variable declarations.
	///
	/// <code>
	/// var (x, y) = expr;
	/// </code>
	/// →
	/// <code>
	/// var tuple = expr;
	/// var x = tuple.Item1;
	/// var y = tuple.Item2;
	/// </code>
	/// </summary>
	public static bool TryDeconstructTupleAssignment(
		ExpressionStatementSyntax statement,
		[NotNullWhen(true)] out SyntaxList<StatementSyntax>? result)
	{
		result = null;

		if (statement.Expression is not AssignmentExpressionSyntax { Left: TupleExpressionSyntax tuple } assignment 
		    || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
		{
			return false;
		}

		if (tuple.Arguments.Count < 2)
		{
			return false;
		}

		var tempName = "_tuple";
		var statements = new SyntaxList<StatementSyntax>();

		// var _tuple = expr;
		statements = statements.Add(
			LocalDeclarationStatement(
				VariableDeclaration(
					IdentifierName("var"),
					SingletonSeparatedList(
						VariableDeclarator(Identifier(tempName))
							.WithInitializer(EqualsValueClause(assignment.Right))))));

		// var x = _tuple.Item1; var y = _tuple.Item2; ...
		for (var i = 0; i < tuple.Arguments.Count; i++)
		{
			var arg = tuple.Arguments[i];

			if (arg.Expression is not IdentifierNameSyntax varId)
			{
				return false;
			}

			statements = statements.Add(
				LocalDeclarationStatement(
					VariableDeclaration(
						IdentifierName("var"),
						SingletonSeparatedList(
							VariableDeclarator(varId.Identifier)
								.WithInitializer(EqualsValueClause(
									MemberAccessExpression(
										SyntaxKind.SimpleMemberAccessExpression,
										IdentifierName(tempName),
										IdentifierName($"Item{i + 1}"))))))));
		}

		result = statements;
		return true;
	}
}

