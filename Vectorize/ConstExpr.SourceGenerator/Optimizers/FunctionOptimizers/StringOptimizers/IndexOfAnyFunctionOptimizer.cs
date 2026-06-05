using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Rewrites <c>string.IndexOfAny</c> over a constant set of characters to a cached
///   <c>System.Buffers.SearchValues&lt;char&gt;</c> lookup — the modern, vectorized BCL path (see CA1870):
///   <code>
///   str.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' })
///     => str.AsSpan().IndexOfAny(SearchValues_xxxx)
/// </code>
///   with a generated
///   <c>private static readonly SearchValues&lt;char&gt; SearchValues_xxxx = SearchValues.Create("aeiou");</c>.
///   The lookup table is built once and cached, avoiding the per-call <c>char[]</c> scan.
/// </summary>
public class IndexOfAnyFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IndexOfAny", false, n => n is 1)
{
	// Only worth a cached SearchValues for sets of at least this many characters;
	// a single character should use IndexOf(char) instead.
	private const int MinCharacters = 2;

	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (Instance is not ExpressionSyntax instanceExpression)
		{
			return false;
		}

		// SearchValues<T> only exists on .NET 8+ — bail when the target framework lacks it.
		if (context.Model.Compilation.GetTypeByMetadataName("System.Buffers.SearchValues`1") is null)
		{
			return false;
		}

		// The argument must be a compile-time constant char[] (new[]{...}, new char[]{...} or [...]).
		if (!TryGetLiteralValue(context.VisitedParameters[0], context, out var value)
		    || value is not char[] { Length: >= MinCharacters } chars)
		{
			return false;
		}

		var charsText = new string(chars);
		var fieldName = $"SearchValues_{charsText.GetDeterministicHashString()}";

		var searchValuesType = GenericName(Identifier("SearchValues"))
			.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(PredefinedType(Token(SyntaxKind.CharKeyword)))));

		// SearchValues.Create("aeiou")
		var createCall = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("SearchValues"), IdentifierName("Create")))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(
				LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(charsText))))));

		// private static readonly SearchValues<char> SearchValues_xxxx = SearchValues.Create("aeiou");
		var field = FieldDeclaration(
				VariableDeclaration(searchValuesType)
					.WithVariables(SingletonSeparatedList(
						VariableDeclarator(Identifier(fieldName))
							.WithInitializer(EqualsValueClause(createCall)))))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));

		context.AdditionalSyntax.Add(field, true);
		context.Usings.Add("System");
		context.Usings.Add("System.Buffers");

		// str.AsSpan().IndexOfAny(SearchValues_xxxx)
		var span = InvocationExpression(
			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, instanceExpression, IdentifierName("AsSpan")));

		result = InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, span, IdentifierName("IndexOfAny")))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(fieldName)))));

		return true;
	}
}