using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts a primary constructor (C# 12) to a regular constructor.
/// Inspired by the Roslyn <c>ConvertPrimaryToRegularConstructorCodeRefactoringProvider</c>.
///
/// <code>
/// class Person(string name, int age) { }
/// </code>
/// →
/// <code>
/// class Person
/// {
///     public Person(string name, int age)
///     {
///     }
/// }
/// </code>
///
/// This is a simplified pure-syntax transformation that moves the parameter list
/// into an explicit constructor. It does not rewrite parameter usages in members.
/// </summary>
public static class ConvertPrimaryToRegularConstructorRefactoring
{
	/// <summary>
	/// Converts a class with a primary constructor to one with a regular constructor.
	/// </summary>
	public static bool TryConvertPrimaryToRegularConstructor(
		ClassDeclarationSyntax classDecl,
		[NotNullWhen(true)] out ClassDeclarationSyntax? result)
	{
		result = null;

		if (classDecl.ParameterList is null or { Parameters.Count: 0 })
		{
			return false;
		}

		var parameters = classDecl.ParameterList;

		// Build the constructor
		var constructor = ConstructorDeclaration(classDecl.Identifier)
			.AddModifiers(Token(SyntaxKind.PublicKeyword))
			.WithParameterList(parameters.WithoutTrivia())
			.WithBody(Block());

		// Remove the parameter list from the class and add the constructor
		result = classDecl
			.WithParameterList(null)
			.WithMembers(classDecl.Members.Insert(0, constructor));

		return true;
	}

	/// <summary>
	/// Converts a record with a primary constructor to one with a regular constructor.
	/// </summary>
	public static bool TryConvertRecordPrimaryToRegularConstructor(
		RecordDeclarationSyntax recordDecl,
		[NotNullWhen(true)] out RecordDeclarationSyntax? result)
	{
		result = null;

		if (recordDecl.ParameterList is null || recordDecl.ParameterList.Parameters.Count == 0)
		{
			return false;
		}

		var parameters = recordDecl.ParameterList;

		// Build the constructor
		var constructor = ConstructorDeclaration(recordDecl.Identifier)
			.AddModifiers(Token(SyntaxKind.PublicKeyword))
			.WithParameterList(parameters.WithoutTrivia())
			.WithBody(Block());

		// Ensure we have braces (some records are declared with semicolons only)
		var openBrace = recordDecl.OpenBraceToken.IsMissing
			? Token(SyntaxKind.OpenBraceToken)
			: recordDecl.OpenBraceToken;

		var closeBrace = recordDecl.CloseBraceToken.IsMissing
			? Token(SyntaxKind.CloseBraceToken)
			: recordDecl.CloseBraceToken;

		result = recordDecl
			.WithParameterList(null)
			.WithOpenBraceToken(openBrace)
			.WithCloseBraceToken(closeBrace)
			.WithSemicolonToken(default)
			.WithMembers(recordDecl.Members.Insert(0, constructor));

		return true;
	}
}

