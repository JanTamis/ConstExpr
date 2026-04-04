using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that encapsulates a field by generating a property that wraps it.
/// Inspired by the Roslyn <c>EncapsulateFieldCodeRefactoringProvider</c>.
///
/// <code>
/// public int count;
/// </code>
/// →
/// <code>
/// private int count;
/// public int Count
/// {
///     get { return count; }
///     set { count = value; }
/// }
/// </code>
///
/// This is a simplified syntax-only transformation that generates a property
/// for a single field declarator.
/// </summary>
public static class EncapsulateFieldRefactoring
{
	/// <summary>
	/// Generates a property to encapsulate a field.
	/// Returns the modified field (made private) and the new property.
	/// </summary>
	public static bool TryEncapsulateField(
		FieldDeclarationSyntax field,
		[NotNullWhen(true)] out FieldDeclarationSyntax? modifiedField,
		[NotNullWhen(true)] out PropertyDeclarationSyntax? property)
	{
		modifiedField = null;
		property = null;

		if (field.Declaration.Variables.Count != 1)
		{
			return false;
		}

		var variable = field.Declaration.Variables[0];
		var fieldName = variable.Identifier.ValueText;
		var type = field.Declaration.Type;

		if (string.IsNullOrEmpty(fieldName))
		{
			return false;
		}

		var propertyName = GeneratePropertyName(fieldName);

		if (propertyName == fieldName)
		{
			// Cannot generate a distinct property name
			return false;
		}

		// Make the field private
		modifiedField = field
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));

		// Build the property
		var getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
			.WithBody(Block(ReturnStatement(IdentifierName(fieldName))));

		var setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
			.WithBody(Block(
				ExpressionStatement(
					AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						IdentifierName(fieldName),
						IdentifierName("value")))));

		property = PropertyDeclaration(type, Identifier(propertyName))
			.AddModifiers(Token(SyntaxKind.PublicKeyword))
			.WithAccessorList(AccessorList(List([ getter, setter ])));

		return true;
	}

	/// <summary>
	/// Generates a PascalCase property name from a field name.
	/// Strips leading underscores and capitalises the first letter.
	/// </summary>
	private static string GeneratePropertyName(string fieldName)
	{
		// Strip leading underscores
		var start = 0;

		while (start < fieldName.Length && fieldName[start] == '_')
		{
			start++;
		}

		if (start >= fieldName.Length)
		{
			return "Value";
		}

		var baseName = fieldName.Substring(start);

		return char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
	}
}

