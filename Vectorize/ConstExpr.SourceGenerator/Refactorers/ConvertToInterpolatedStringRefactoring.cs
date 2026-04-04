using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts string concatenation into an interpolated string.
/// Inspired by the Roslyn <c>ConvertConcatenationToInterpolatedStringCodeRefactoringProvider</c>.
///
/// <code>
/// "Hello, " + name + "!"
/// </code>
/// →
/// <code>
/// $"Hello, {name}!"
/// </code>
///
/// Handles chains of <c>+</c> operators where at least one operand is a string literal.
/// </summary>
public static class ConvertToInterpolatedStringRefactoring
{
	/// <summary>
	/// Tries to convert a string concatenation expression into an interpolated string.
	/// The expression must be a chain of <c>+</c> operators containing at least one string literal.
	/// When a <paramref name="semanticModel"/> is provided, verifies that the <c>+</c> operator
	/// is truly a string concatenation (result type is <c>string</c>).
	/// </summary>
	public static bool TryConvertConcatenationToInterpolatedString(
		BinaryExpressionSyntax concatenation,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out InterpolatedStringExpressionSyntax? result)
	{
		result = null;

		if (!concatenation.IsKind(SyntaxKind.AddExpression))
		{
			return false;
		}

		// Verify the + is a string concatenation
		var typeInfo = semanticModel.GetTypeInfo(concatenation).Type;

		if (typeInfo is null || typeInfo.SpecialType != SpecialType.System_String)
		{
			return false;
		}

		// Collect all parts of the concatenation chain
		var parts = new List<ExpressionSyntax>();
		CollectConcatenationParts(concatenation, parts);

		// At least one part must be a string literal
		var hasStringLiteral = false;

		foreach (var part in parts)
		{
			if (part is LiteralExpressionSyntax literal &&
			    literal.IsKind(SyntaxKind.StringLiteralExpression))
			{
				hasStringLiteral = true;
				break;
			}
		}

		if (!hasStringLiteral)
		{
			return false;
		}

		if (parts.Count < 2)
		{
			return false;
		}

		// Build interpolated string contents
		var contents = new List<InterpolatedStringContentSyntax>();

		foreach (var part in parts)
		{
			if (part is LiteralExpressionSyntax literal
			    && literal.IsKind(SyntaxKind.StringLiteralExpression))
			{
				var text = literal.Token.ValueText;

				if (text.Length > 0)
				{
					contents.Add(InterpolatedStringText(
						Token(
							default,
							SyntaxKind.InterpolatedStringTextToken,
							EscapeForInterpolation(text),
							text,
							default)));
				}
			}
			else
			{
				contents.Add(Interpolation(part.WithoutTrivia()));
			}
		}

		result = InterpolatedStringExpression(
				Token(SyntaxKind.InterpolatedStringStartToken),
				List(contents),
				Token(SyntaxKind.InterpolatedStringEndToken))
			.WithTriviaFrom(concatenation);

		return true;
	}

	/// <summary>
	/// Flattens a chain of <c>+</c> binary expressions into a list of operands.
	/// </summary>
	private static void CollectConcatenationParts(ExpressionSyntax expression, List<ExpressionSyntax> parts)
	{
		if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
		{
			CollectConcatenationParts(binary.Left, parts);
			CollectConcatenationParts(binary.Right, parts);
		}
		else
		{
			parts.Add(expression);
		}
	}

	/// <summary>
	/// Escapes characters that are special in interpolated string text: <c>{</c>, <c>}</c>.
	/// </summary>
	private static string EscapeForInterpolation(string text)
	{
		if (text.IndexOfAny([ '{', '}' ]) < 0)
		{
			return text;
		}

		var sb = new StringBuilder(text.Length + 4);

		foreach (var c in text)
		{
			switch (c)
			{
				case '{':
				{
					sb.Append("{{");
					break;
				}
				case '}':
				{
					sb.Append("}}");
					break;
				}
				default:
				{
					sb.Append(c);
					break;
				}
			}
		}

		return sb.ToString();
	}
}