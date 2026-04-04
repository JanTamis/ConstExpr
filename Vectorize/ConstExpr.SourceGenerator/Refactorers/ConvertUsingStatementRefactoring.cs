using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that introduces or removes a using/declaration for IDisposable objects.
/// Inspired by the Roslyn <c>IntroduceUsingStatementCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Converts a using-statement to a using-declaration (C# 8+):
///     <code>
///     using (var x = Expr()) { Body(); }
///     </code>
///     →
///     <code>
///     using var x = Expr();
///     Body();
///     </code>
///   </item>
///   <item>Converts a using-declaration back to a using-statement (reverse).</item>
/// </list>
/// </summary>
public static class ConvertUsingStatementRefactoring
{
	/// <summary>
	/// Converts a using-statement with a declaration to a using-declaration (C# 8+).
	/// </summary>
	public static bool TryConvertToUsingDeclaration(
		UsingStatementSyntax usingStatement,
		[NotNullWhen(true)] out SyntaxList<StatementSyntax>? result)
	{
		result = null;

		if (usingStatement.Declaration is null)
		{
			return false;
		}

		// Build: using var x = expr;
		var usingDecl = LocalDeclarationStatement(usingStatement.Declaration)
			.WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
			.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
			.WithLeadingTrivia(usingStatement.GetLeadingTrivia());

		var statements = new SyntaxList<StatementSyntax>();
		statements = statements.Add(usingDecl);

		// Unwrap the body
		if (usingStatement.Statement is BlockSyntax block)
		{
			foreach (var stmt in block.Statements)
			{
				statements = statements.Add(stmt);
			}
		}
		else
		{
			statements = statements.Add(usingStatement.Statement);
		}

		result = statements;
		return true;
	}

	/// <summary>
	/// Converts a using-declaration back to a using-statement.
	/// The caller must collect the statements following the using-declaration that should
	/// go inside the using-statement body.
	/// </summary>
	public static bool TryConvertToUsingStatement(
		LocalDeclarationStatementSyntax usingDeclaration,
		SyntaxList<StatementSyntax> followingStatements,
		[NotNullWhen(true)] out UsingStatementSyntax? result)
	{
		result = null;

		if (!usingDeclaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
		{
			return false;
		}

		result = UsingStatement(
				usingDeclaration.Declaration,
				null,
				Block(followingStatements))
			.WithUsingKeyword(Token(SyntaxKind.UsingKeyword))
			.WithLeadingTrivia(usingDeclaration.GetLeadingTrivia());

		return true;
	}
}

