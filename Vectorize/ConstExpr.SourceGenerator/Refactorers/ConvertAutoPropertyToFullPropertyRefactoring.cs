using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts an auto-property to a full property with a backing field.
/// Inspired by the Roslyn <c>CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider</c>.
///
/// <code>
/// public string Name { get; set; }
/// </code>
/// →
/// <code>
/// private string _name;
/// public string Name
/// {
///     get { return _name; }
///     set { _name = value; }
/// }
/// </code>
///
/// This is a pure syntax-level transformation; it generates a conventional <c>_camelCase</c>
/// backing field name from the property name.
/// </summary>
public static class ConvertAutoPropertyToFullPropertyRefactoring
{
	/// <summary>
	/// Converts an auto-implemented property to a full property with explicit get/set accessors
	/// and a backing field declaration.
	/// </summary>
	/// <param name="property">The auto-property to convert.</param>
	/// <param name="fullProperty">The rewritten property with explicit accessors.</param>
	/// <param name="backingField">The generated backing field declaration.</param>
	public static bool TryConvertAutoPropertyToFullProperty(
		PropertyDeclarationSyntax property,
		[NotNullWhen(true)] out PropertyDeclarationSyntax? fullProperty,
		[NotNullWhen(true)] out FieldDeclarationSyntax? backingField)
	{
		fullProperty = null;
		backingField = null;

		// Must be an auto-property (has accessor list, no bodies)
		if (property.AccessorList is null)
		{
			return false;
		}

		var getter = FindAccessor(property, SyntaxKind.GetAccessorDeclaration);
		var setter = FindAccessor(property, SyntaxKind.SetAccessorDeclaration)
		             ?? FindAccessor(property, SyntaxKind.InitAccessorDeclaration);

		// Must have at least a getter with no body (auto-implemented)
		if (getter is null 
		    || getter.Body is not null 
		    || getter.ExpressionBody is not null)
		{
			return false;
		}

		var fieldName = GenerateBackingFieldName(property.Identifier.ValueText);
		var type = property.Type;

		// Build backing field
		var fieldDeclarator = VariableDeclarator(Identifier(fieldName));

		if (property.Initializer is not null)
		{
			fieldDeclarator = fieldDeclarator.WithInitializer(property.Initializer);
		}

		backingField = FieldDeclaration(
				VariableDeclaration(type, SingletonSeparatedList(fieldDeclarator)))
			.AddModifiers(Token(SyntaxKind.PrivateKeyword));

		// Build get accessor
		var getBody = Block(ReturnStatement(IdentifierName(fieldName)));

		var newGetter = getter
			.WithBody(getBody)
			.WithSemicolonToken(default);

		// Build set/init accessor (if present)
		AccessorDeclarationSyntax? newSetter = null;

		if (setter is not null 
		    && setter.Body is null 
		    && setter.ExpressionBody is null)
		{
			var setBody = Block(
				ExpressionStatement(
					AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						IdentifierName(fieldName),
						IdentifierName("value"))));

			newSetter = setter
				.WithBody(setBody)
				.WithSemicolonToken(default);
		}

		// Build accessor list
		var accessors = new SyntaxList<AccessorDeclarationSyntax>();
		accessors = accessors.Add(newGetter);

		if (newSetter is not null)
		{
			accessors = accessors.Add(newSetter);
		}

		fullProperty = property
			.WithAccessorList(AccessorList(accessors))
			.WithInitializer(null)
			.WithSemicolonToken(default);

		return true;
	}

	/// <summary>
	/// Converts a full property (with get/set that read/write a backing field) back to
	/// an auto-property.
	/// </summary>
	public static bool TryConvertFullPropertyToAutoProperty(
		PropertyDeclarationSyntax property,
		[NotNullWhen(true)] out PropertyDeclarationSyntax? autoProperty)
	{
		autoProperty = null;

		if (property.AccessorList is null)
		{
			return false;
		}

		var getter = FindAccessor(property, SyntaxKind.GetAccessorDeclaration);

		// Getter must have a body with a single return of an identifier
		if (getter?.Body is not { Statements: [ ReturnStatementSyntax { Expression: IdentifierNameSyntax fieldRef } ] })
		{
			return false;
		}

		var autoGetter = getter
			.WithBody(null)
			.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

		var accessors = new SyntaxList<AccessorDeclarationSyntax>();
		accessors = accessors.Add(autoGetter);

		var setter = FindAccessor(property, SyntaxKind.SetAccessorDeclaration)
		             ?? FindAccessor(property, SyntaxKind.InitAccessorDeclaration);

		if (setter?.Body is { Statements: [ ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: IdentifierNameSyntax assignTarget } } ] } 
		    && assignTarget.Identifier.ValueText == fieldRef.Identifier.ValueText)
		{
			var autoSetter = setter
				.WithBody(null)
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
			accessors = accessors.Add(autoSetter);
		}

		autoProperty = property
			.WithAccessorList(AccessorList(accessors));

		return true;
	}

	private static AccessorDeclarationSyntax? FindAccessor(PropertyDeclarationSyntax property, SyntaxKind kind)
	{
		if (property.AccessorList is null)
		{
			return null;
		}

		foreach (var accessor in property.AccessorList.Accessors)
		{
			if (accessor.IsKind(kind))
			{
				return accessor;
			}
		}

		return null;
	}

	/// <summary>
	/// Generates a backing field name from a property name using the <c>_camelCase</c> convention.
	/// </summary>
	private static string GenerateBackingFieldName(string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName))
		{
			return "_field";
		}

		return $"_{Char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";
	}
}