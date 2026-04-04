using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between block-scoped and file-scoped namespace declarations.
/// Inspired by the Roslyn <c>ConvertNamespaceCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Block-scoped → file-scoped:
///     <code>
///     namespace Foo
///     {
///         class Bar { }
///     }
///     </code>
///     →
///     <code>
///     namespace Foo;
///     class Bar { }
///     </code>
///   </item>
///   <item>File-scoped → block-scoped (reverse)</item>
/// </list>
///
/// Conversion to file-scoped requires that the compilation unit contains exactly one namespace
/// and that the namespace is not nested.
/// </summary>
public static class ConvertNamespaceRefactoring
{
	/// <summary>
	/// Converts a block-scoped namespace to a file-scoped namespace.
	/// Only succeeds when the compilation unit contains exactly one namespace at the top level.
	/// </summary>
	public static bool TryConvertToFileScopedNamespace(
		NamespaceDeclarationSyntax namespaceDecl,
		[NotNullWhen(true)] out FileScopedNamespaceDeclarationSyntax? result)
	{
		result = null;

		// Cannot convert if there are nested namespaces
		if (namespaceDecl.Members.OfType<BaseNamespaceDeclarationSyntax>().Any())
		{
			return false;
		}

		result = FileScopedNamespaceDeclaration(
				namespaceDecl.AttributeLists,
				namespaceDecl.Modifiers,
				namespaceDecl.Name,
				namespaceDecl.Externs,
				namespaceDecl.Usings,
				namespaceDecl.Members)
			.WithNamespaceKeyword(namespaceDecl.NamespaceKeyword)
			.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
			.WithLeadingTrivia(namespaceDecl.GetLeadingTrivia())
			.WithTrailingTrivia(namespaceDecl.GetTrailingTrivia());

		return true;
	}

	/// <summary>
	/// Converts a file-scoped namespace to a block-scoped namespace.
	/// </summary>
	public static bool TryConvertToBlockScopedNamespace(
		FileScopedNamespaceDeclarationSyntax fileScopedDecl,
		[NotNullWhen(true)] out NamespaceDeclarationSyntax? result)
	{
		result = NamespaceDeclaration(
				fileScopedDecl.Name)
			.WithAttributeLists(fileScopedDecl.AttributeLists)
			.WithModifiers(fileScopedDecl.Modifiers)
			.WithNamespaceKeyword(fileScopedDecl.NamespaceKeyword)
			.WithExterns(fileScopedDecl.Externs)
			.WithUsings(fileScopedDecl.Usings)
			.WithMembers(fileScopedDecl.Members)
			.WithLeadingTrivia(fileScopedDecl.GetLeadingTrivia())
			.WithTrailingTrivia(fileScopedDecl.GetTrailingTrivia());

		return true;
	}
}

