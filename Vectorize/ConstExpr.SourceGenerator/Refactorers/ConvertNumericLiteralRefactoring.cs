using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts numeric literals between decimal, hexadecimal, and binary forms.
/// Inspired by the Roslyn <c>ConvertNumericLiteralCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item>Decimal → hex:    <c>255</c>  →  <c>0xFF</c></item>
///   <item>Decimal → binary: <c>255</c>  →  <c>0b11111111</c></item>
///   <item>Hex → decimal:    <c>0xFF</c>  →  <c>255</c></item>
///   <item>Binary → decimal: <c>0b11111111</c>  →  <c>255</c></item>
/// </list>
/// </summary>
public static class ConvertNumericLiteralRefactoring
{
	private const string HexPrefix = "0x";
	private const string HexPrefixUpper = "0X";
	private const string BinaryPrefix = "0b";
	private const string BinaryPrefixUpper = "0B";

	/// <summary>
	/// Converts a numeric literal to its hexadecimal representation.
	/// </summary>
	public static bool TryConvertToHex(
		LiteralExpressionSyntax literal,
		[NotNullWhen(true)] out LiteralExpressionSyntax? result)
	{
		result = null;

		if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
		{
			return false;
		}

		var text = literal.Token.Text;

		// Already hex
		if (text.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!TryGetIntegralValue(literal.Token, out var value))
		{
			return false;
		}

		var suffix = GetSuffix(text);
		var hexText = HexPrefix + value.ToString("X") + suffix;

		result = CreateLiteral(literal, hexText, literal.Token.Value!);
		return true;
	}

	/// <summary>
	/// Converts a numeric literal to its binary representation.
	/// </summary>
	public static bool TryConvertToBinary(
		LiteralExpressionSyntax literal,
		[NotNullWhen(true)] out LiteralExpressionSyntax? result)
	{
		result = null;

		if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
		{
			return false;
		}

		var text = literal.Token.Text;

		// Already binary
		if (text.StartsWith(BinaryPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!TryGetIntegralValue(literal.Token, out var value))
		{
			return false;
		}

		var suffix = GetSuffix(text);
		var binaryText = BinaryPrefix + Convert.ToString(value, 2) + suffix;

		result = CreateLiteral(literal, binaryText, literal.Token.Value!);
		return true;
	}

	/// <summary>
	/// Converts a numeric literal to its decimal representation.
	/// </summary>
	public static bool TryConvertToDecimal(
		LiteralExpressionSyntax literal,
		[NotNullWhen(true)] out LiteralExpressionSyntax? result)
	{
		result = null;

		if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
		{
			return false;
		}

		var text = literal.Token.Text;

		// Already decimal (no prefix)
		if (!text.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase) 
		    && !text.StartsWith(BinaryPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!TryGetIntegralValue(literal.Token, out var value))
		{
			return false;
		}

		var suffix = GetSuffix(text);
		var decimalText = value.ToString(CultureInfo.InvariantCulture) + suffix;

		result = CreateLiteral(literal, decimalText, literal.Token.Value!);
		return true;
	}

	// -----------------------------------------------------------------------
	// Private helpers
	// -----------------------------------------------------------------------

	private static bool TryGetIntegralValue(SyntaxToken token, out long value)
	{
		value = 0;

		switch (token.Value)
		{
			case int i:
			{
				value = i;
				return true;
			}
			case long l:
			{
				value = l;
				return true;
			}
			case uint u:
			{
				value = u;
				return true;
			}
			// Only convert if it fits in long
			case ulong ul and <= long.MaxValue:
			{
				value = (long)ul;
				return true;
			}
			case ulong:
			{
				return false;
			}
			case short s:
			{
				value = s;
				return true;
			}
			case ushort us:
			{
				value = us;
				return true;
			}
			case byte b:
			{
				value = b;
				return true;
			}
			case sbyte sb2:
			{
				value = sb2;
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Extracts the type suffix (L, UL, U, etc.) from the literal text.
	/// </summary>
	private static string GetSuffix(string text)
	{
		// Remove underscores for analysis
		var clean = text.Replace("_", "");
		var i = clean.Length - 1;

		while (i >= 0 && char.IsLetter(clean[i]) && !IsHexDigit(clean[i]))
		{
			i--;
		}

		return i < clean.Length - 1 ? clean[(i + 1)..] : "";
	}

	private static bool IsHexDigit(char c)
	{
		return c is (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
	}

	private static LiteralExpressionSyntax CreateLiteral(
		LiteralExpressionSyntax original, string text, object value)
	{
		SyntaxToken token;

		switch (value)
		{
			case int intVal:
			{
				token = Literal(original.Token.LeadingTrivia, text, intVal, original.Token.TrailingTrivia);
				break;
			}
			case long longVal:
			{
				token = Literal(original.Token.LeadingTrivia, text, longVal, original.Token.TrailingTrivia);
				break;
			}
			case uint uintVal:
			{
				token = Literal(original.Token.LeadingTrivia, text, uintVal, original.Token.TrailingTrivia);
				break;
			}
			case ulong ulongVal:
			{
				token = Literal(original.Token.LeadingTrivia, text, ulongVal, original.Token.TrailingTrivia);
				break;
			}
			default:
			{
				return original;
			}
		}

		return LiteralExpression(SyntaxKind.NumericLiteralExpression, token);
	}
}

