using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between direct casts and try-casts (as-casts).
/// Inspired by the Roslyn <c>ConvertDirectCastToTryCastCodeRefactoringProvider</c>
/// and <c>ConvertTryCastToDirectCastCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Direct → try:  <c>(Type)expr</c>  →  <c>expr as Type</c></item>
///   <item>Try → direct:  <c>expr as Type</c>  →  <c>(Type)expr</c></item>
/// </list>
/// </summary>
public static class ConvertCastRefactoring
{
	/// <summary>
	/// Converts a direct cast expression <c>(T)expr</c> to a try-cast <c>expr as T</c>.
	/// Only applies to reference-type-like casts (not numeric casts).
	/// </summary>
	public static bool TryConvertDirectCastToTryCast(
		CastExpressionSyntax castExpression,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out BinaryExpressionSyntax? result)
	{
		result = null;

		var type = castExpression.Type;
		var expression = castExpression.Expression;

		// Skip casts to value types (int, double, etc.) — 'as' doesn't work with value types
		if (IsKnownValueType(type, semanticModel))
		{
			return false;
		}

		result = BinaryExpression(
				SyntaxKind.AsExpression,
				expression.WithoutTrivia(),
				type.WithoutTrivia())
			.WithTriviaFrom(castExpression);

		return true;
	}

	/// <summary>
	/// Converts a try-cast expression <c>expr as T</c> to a direct cast <c>(T)expr</c>.
	/// </summary>
	public static bool TryConvertTryCastToDirectCast(
		BinaryExpressionSyntax asExpression,
		[NotNullWhen(true)] out CastExpressionSyntax? result)
	{
		result = null;

		if (!asExpression.IsKind(SyntaxKind.AsExpression))
		{
			return false;
		}

		if (asExpression.Right is not TypeSyntax type)
		{
			return false;
		}

		result = CastExpression(type.WithoutTrivia(), asExpression.Left.WithoutTrivia())
			.WithTriviaFrom(asExpression);

		return true;
	}

	/// <summary>
	/// Converts a cast expression <c>(T)expr</c> to an is-pattern with declaration:
	/// produces the expression <c>expr is T name</c>.
	/// The caller must supply the desired variable name.
	/// </summary>
	public static bool TryConvertCastToIsPattern(
		CastExpressionSyntax castExpression,
		string variableName,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out IsPatternExpressionSyntax? result)
	{
		result = null;

		var type = castExpression.Type;
		var expression = castExpression.Expression;

		if (IsKnownValueType(type, semanticModel))
		{
			return false;
		}

		var pattern = DeclarationPattern(
			type.WithoutTrivia(),
			SingleVariableDesignation(Identifier(variableName)));

		result = IsPatternExpression(expression.WithoutTrivia(), pattern)
			.WithTriviaFrom(castExpression);

		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> for value types where <c>as</c> cannot be used.
	/// When a <see cref="SemanticModel"/> is available, resolves the type symbol to
	/// handle user-defined structs and enums in addition to predefined types.
	/// </summary>
	private static bool IsKnownValueType(TypeSyntax type, SemanticModel semanticModel)
	{
		var typeInfo = semanticModel.GetTypeInfo(type).Type;

		if (typeInfo is not null)
		{
			return typeInfo.IsValueType;
		}

		// Fallback to syntactic check for predefined types
		if (type is not PredefinedTypeSyntax predefined)
		{
			return false;
		}

		return predefined.Keyword.Kind() switch
		{
			SyntaxKind.IntKeyword => true,
			SyntaxKind.LongKeyword => true,
			SyntaxKind.ShortKeyword => true,
			SyntaxKind.ByteKeyword => true,
			SyntaxKind.SByteKeyword => true,
			SyntaxKind.UIntKeyword => true,
			SyntaxKind.ULongKeyword => true,
			SyntaxKind.UShortKeyword => true,
			SyntaxKind.FloatKeyword => true,
			SyntaxKind.DoubleKeyword => true,
			SyntaxKind.DecimalKeyword => true,
			SyntaxKind.BoolKeyword => true,
			SyntaxKind.CharKeyword => true,
			_ => false
		};
	}
}