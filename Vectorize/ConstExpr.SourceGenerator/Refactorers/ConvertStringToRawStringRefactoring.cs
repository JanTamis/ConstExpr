using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

/// <summary>
/// Refactorer that converts regular C# string literals to raw string literals (C# 11+),
/// inspired by the Roslyn <c>ConvertStringToRawStringCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Single-line raw strings (<c>"""..."""</c>) — when the value contains no newlines.</item>
///   <item>Multi-line raw strings — when the value contains newline characters.</item>
/// </list>
///
/// Conversion is only applied when the literal source text actually contains escape sequences,
/// so that unescaped strings (e.g. <c>"hello"</c>) are left unchanged.
/// </summary>
public static class ConvertStringToRawStringRefactoring
{
	/// <summary>
	/// Tries to convert a regular string literal node to a raw string literal.
	/// </summary>
	/// <param name="node">The literal expression to inspect.</param>
	/// <param name="result">
	/// When this method returns <see langword="true"/>, a semantically equivalent
	/// raw-string literal expression; otherwise <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the literal was converted; <see langword="false"/> if the
	/// literal is already a raw string, is a verbatim string, or contains no escape sequences.
	/// </returns>
	public static bool TryConvertToRawString(
		LiteralExpressionSyntax node,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (!node.IsKind(SyntaxKind.StringLiteralExpression))
		{
			return false;
		}

		var token = node.Token;

		// Already a raw string — nothing to do.
		if (token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) ||
		    token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
		{
			return false;
		}

		// Skip verbatim strings (@"...") — they are already human-friendly.
		var text = token.Text;
		if (text.Length > 0 && text[0] == '@')
		{
			return false;
		}

		// Only convert when the source text contains an escape sequence.
		if (!ContainsEscapeSequence(text))
		{
			return false;
		}

		var value = token.ValueText;

		result = ContainsNewline(value)
			? CreateMultiLineRawStringLiteral(value, token)
			: CreateSingleLineRawStringLiteral(value, token);

		return true;
	}

	// -----------------------------------------------------------------------
	// Private helpers
	// -----------------------------------------------------------------------

	/// <summary>
	/// Returns <see langword="true"/> when the source text of a string literal contains at
	/// least one backslash escape, indicating the raw-string form would be more readable.
	/// </summary>
	private static bool ContainsEscapeSequence(string sourceText)
	{
		// sourceText includes the surrounding quotes, e.g. "hello\nworld"
		// We scan the interior for backslashes.
		for (var i = 1; i < sourceText.Length - 1; i++)
		{
			if (sourceText[i] == '\\')
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the string value contains a newline character,
	/// requiring a multi-line raw string literal.
	/// </summary>
	private static bool ContainsNewline(string value)
	{
		return value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
	}

	/// <summary>
	/// Determines the minimum number of double-quote characters needed for the raw-string
	/// delimiter so that the delimiter sequence does not appear inside <paramref name="value"/>.
	/// The result is always at least 3.
	/// </summary>
	private static int ComputeDelimiterLength(string value)
	{
		var quoteCount = 3;

		while (ContainsQuoteSequence(value, quoteCount))
		{
			quoteCount++;
		}

		return quoteCount;
	}

	private static bool ContainsQuoteSequence(string value, int length)
	{
		var sequence = new string('"', length);

		return value.IndexOf(sequence, StringComparison.Ordinal) >= 0;
	}

	/// <summary>
	/// Builds a single-line raw string literal, e.g. <c>"""content"""</c>.
	/// Used when the string value does not contain newlines.
	/// </summary>
	private static ExpressionSyntax CreateSingleLineRawStringLiteral(string value, SyntaxToken original)
	{
		var delimLen = ComputeDelimiterLength(value);
		var delimiter = new string('"', delimLen);
		var rawText = delimiter + value + delimiter;

		var rawToken = Token(
			original.LeadingTrivia,
			SyntaxKind.SingleLineRawStringLiteralToken,
			rawText,
			value,
			original.TrailingTrivia);

		return LiteralExpression(SyntaxKind.StringLiteralExpression, rawToken);
	}

	/// <summary>
	/// Builds a multi-line raw string literal.
	/// The produced form is:
	/// <code>
	/// """
	/// &lt;line 1&gt;
	/// &lt;line 2&gt;
	/// """
	/// </code>
	/// The trailing newline before the closing delimiter is stripped by the C# compiler,
	/// so the resulting value is semantically identical to the original.
	/// </summary>
	private static ExpressionSyntax CreateMultiLineRawStringLiteral(string value, SyntaxToken original)
	{
		// Normalise line endings so the raw text uses only '\n'.
		var normalised = value.Replace("\r\n", "\n").Replace("\r", "\n");

		var delimLen = ComputeDelimiterLength(normalised);
		var delimiter = new string('"', delimLen);

		// The C# compiler strips the newline immediately before the closing delimiter
		// and the leading whitespace of each content line that matches the closing
		// delimiter's indentation. Using no indentation keeps the logic simple and
		// the value semantically identical to the original.
		//
		// Token text layout: """<NL><content><NL>"""
		// The compiler strips the trailing <NL> → value == normalised (without trailing NL).
		var sb = new StringBuilder(delimiter.Length * 2 + normalised.Length + 2);
		sb.Append(delimiter);
		sb.Append('\n');
		sb.Append(normalised);
		sb.Append('\n');
		sb.Append(delimiter);
		var rawText = sb.ToString();

		var rawToken = Token(
			original.LeadingTrivia,
			SyntaxKind.MultiLineRawStringLiteralToken,
			rawText,
			normalised,
			original.TrailingTrivia);

		return LiteralExpression(SyntaxKind.StringLiteralExpression, rawToken);
	}
}





