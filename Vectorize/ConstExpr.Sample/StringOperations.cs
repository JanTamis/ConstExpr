using ConstExpr.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class StringOperations
{
	public static int StringLength(string value, Encoding encoding)
	{
		return encoding.GetByteCount(value);
	}

	public static ReadOnlySpan<byte> StringBytes(string value, Encoding encoding)
	{
		return encoding.GetBytes(value);
	}

	public static ICharCollection Base64Encode(string value)
	{
		return (ICharCollection)(object)Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
	}

	public static IReadOnlyList<string> Split(string value, char separator)
	{
		return value.Split([separator], StringSplitOptions.TrimEntries);
	}

	public static string InterpolationTest(string name, int age, double height)
	{
		return $"Name: {name}, Age: {age}, Height: {height:N0} cm";
	}

	public static string FormatFullName(string firstName, string middleName, string lastName, bool includeMiddle)
	{
		if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
		{
			throw new ArgumentException("First and last name are required");
		}

		if (includeMiddle && !string.IsNullOrWhiteSpace(middleName))
		{
			return $"{firstName} {middleName} {lastName}";
		}

		return $"{firstName} {lastName}";
	}

	public static string GenerateSlug(string text, int maxLength, char separator, bool toLowerCase)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		var result = text.Trim();

		if (toLowerCase)
		{
			result = result.ToLowerInvariant();
		}

		result = result.Replace(' ', separator);
		result = new string(result.Where(c => char.IsLetterOrDigit(c) || c == separator).ToArray());

		if (result.Length > maxLength && maxLength > 0)
		{
			result = result.Substring(0, maxLength);
		}

		return result;
	}

	// Additional string operations
	public static string Reverse(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		return new string(text.Reverse().ToArray());
	}

	public static bool IsPalindrome(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return true;
		}

		var cleaned = text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
		var length = cleaned.Length;

		for (var i = 0; i < length / 2; i++)
		{
			if (cleaned[i] != cleaned[length - 1 - i])
			{
				return false;
			}
		}

		return true;
	}

	public static int CountOccurrences(string text, string substring)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring))
		{
			return 0;
		}

		var count = 0;
		var index = 0;

		while ((index = text.IndexOf(substring, index)) != -1)
		{
			count++;
			index += substring.Length;
		}

		return count;
	}

	public static string RemoveWhitespace(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		return new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
	}

	public static string ToCamelCase(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var words = text.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);

		if (words.Length == 0)
		{
			return string.Empty;
		}

		var result = words[0].ToLowerInvariant();

		for (var i = 1; i < words.Length; i++)
		{
			if (words[i].Length > 0)
			{
				result += char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
			}
		}

		return result;
	}

	public static string ToPascalCase(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var words = text.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
		var result = string.Empty;

		foreach (var word in words)
		{
			if (word.Length > 0)
			{
				result += char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
			}
		}

		return result;
	}

	public static string ToSnakeCase(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var words = text.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
		return string.Join("_", words.Select(w => w.ToLowerInvariant()));
	}

	public static string ToKebabCase(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var words = text.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries);
		return string.Join("-", words.Select(w => w.ToLowerInvariant()));
	}

	public static string Truncate(string text, int maxLength, string suffix = "...")
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
		{
			return text;
		}

		var truncatedLength = maxLength - suffix.Length;

		if (truncatedLength <= 0)
		{
			return text.Substring(0, maxLength);
		}

		return text.Substring(0, truncatedLength) + suffix;
	}

	public static int LevenshteinDistance(string a, string b)
	{
		if (string.IsNullOrEmpty(a))
		{
			return string.IsNullOrEmpty(b) ? 0 : b.Length;
		}

		if (string.IsNullOrEmpty(b))
		{
			return a.Length;
		}

		var m = a.Length;
		var n = b.Length;
		var d = new int[m + 1, n + 1];

		for (var i = 0; i <= m; i++)
		{
			d[i, 0] = i;
		}

		for (var j = 0; j <= n; j++)
		{
			d[0, j] = j;
		}

		for (var j = 1; j <= n; j++)
		{
			for (var i = 1; i <= m; i++)
			{
				var cost = a[i - 1] == b[j - 1] ? 0 : 1;
				d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
			}
		}

		return d[m, n];
	}

	public static string RepeatString(string text, int count)
	{
		if (count < 0)
		{
			throw new ArgumentException("Count cannot be negative");
		}

		if (string.IsNullOrEmpty(text) || count == 0)
		{
			return string.Empty;
		}

		var result = new StringBuilder(text.Length * count);

		for (var i = 0; i < count; i++)
		{
			result.Append(text);
		}

		return result.ToString();
	}
}

