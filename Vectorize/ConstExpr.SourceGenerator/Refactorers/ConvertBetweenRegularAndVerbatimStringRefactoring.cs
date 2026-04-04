using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts between regular and verbatim string literals.
/// Inspired by the Roslyn <c>ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Regular → verbatim:  <c>"line1\nline2"</c>  →  <c>@"line1
/// line2"</c></item>
///   <item>Verbatim → regular:  <c>@"line1
/// line2"</c>  →  <c>"line1\nline2"</c></item>
/// </list>
/// </summary>
public static class ConvertBetweenRegularAndVerbatimStringRefactoring
{
	/// <summary>
	/// Converts a regular string literal to a verbatim string literal.
	/// Only converts when the regular string contains escape sequences that
	/// would be simplified in verbatim form.
	/// </summary>
	public static bool TryConvertToVerbatimString(
		LiteralExpressionSyntax literal,
		[NotNullWhen(true)] out LiteralExpressionSyntax? result)
	{
		result = null;

		if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			return false;
		}

		var token = literal.Token;
		var text = token.Text;

		// Must be a regular string (not verbatim, not raw)
		if (text.Length == 0 || text[0] == '@')
		{
			return false;
		}

		if (token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
		    || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
		{
			return false;
		}

		// Only convert if there are escape sequences to simplify
		if (!ContainsConvertibleEscape(text))
		{
			return false;
		}

		var value = token.ValueText;

		// Build the verbatim string: @"..." with doubled quotes
		var sb = new StringBuilder(value.Length + 4);
		sb.Append("@\"");

		foreach (var c in value)
		{
			if (c == '"')
			{
				sb.Append("\"\"");
			}
			else
			{
				sb.Append(c);
			}
		}

		sb.Append('"');

		var newToken = Token(
			token.LeadingTrivia,
			SyntaxKind.StringLiteralToken,
			sb.ToString(),
			value,
			token.TrailingTrivia);

		result = LiteralExpression(SyntaxKind.StringLiteralExpression, newToken);
		return true;
	}

	/// <summary>
	/// Converts a verbatim string literal to a regular string literal.
	/// </summary>
	public static bool TryConvertToRegularString(
		LiteralExpressionSyntax literal,
		[NotNullWhen(true)] out LiteralExpressionSyntax? result)
	{
		result = null;

		if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
		{
			return false;
		}

		var token = literal.Token;
		var text = token.Text;

		// Must be a verbatim string (@"...")
		if (text.Length < 3 || text[0] != '@')
		{
			return false;
		}

		var value = token.ValueText;

		// Check for characters that cannot be represented in a regular string without
		// complex unicode escapes (null bytes). Skip those.
		if (value.IndexOf('\0') >= 0)
		{
			return false;
		}

		var sb = new StringBuilder(value.Length + 4);
		sb.Append('"');

		foreach (var c in value)
		{
			switch (c)
			{
				case '\\':
				{
					sb.Append(@"\\");
					break;
				}
				case '"':
				{
					sb.Append("\\\"");
					break;
				}
				case '\n':
				{
					sb.Append(@"\n");
					break;
				}
				case '\r':
				{
					sb.Append(@"\r");
					break;
				}
				case '\t':
				{
					sb.Append(@"\t");
					break;
				}
				case '\a':
				{
					sb.Append(@"\a");
					break;
				}
				case '\b':
				{
					sb.Append(@"\b");
					break;
				}
				case '\f':
				{
					sb.Append(@"\f");
					break;
				}
				case '\v':
				{
					sb.Append(@"\v");
					break;
				}
				default:
				{
					sb.Append(c);
					break;
				}
			}
		}

		sb.Append('"');

		var newToken = Token(
			token.LeadingTrivia,
			SyntaxKind.StringLiteralToken,
			sb.ToString(),
			value,
			token.TrailingTrivia);

		result = LiteralExpression(SyntaxKind.StringLiteralExpression, newToken);
		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the regular string text contains escape sequences
	/// (other than <c>\0</c>) that would become simpler in verbatim form.
	/// </summary>
	private static bool ContainsConvertibleEscape(string text)
	{
		for (var i = 1; i < text.Length - 1; i++)
		{
			if (text[i] != '\\')
			{
				continue;
			}

			if (i + 1 < text.Length - 1)
			{
				var next = text[i + 1];

				// \0 cannot be represented in a verbatim string
				if (next == '0')
				{
					continue;
				}

				// Any other escape is convertible
				return true;
			}
		}

		return false;
	}
}