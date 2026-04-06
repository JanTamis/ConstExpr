using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Sample.Operations;

[ConstExpr(
	MathOptimizations = FastMathFlags.FastMath,
	LinqOptimisationMode = LinqOptimisationMode.Unroll)]
public static class RegexOperations
{
	/// <summary>
	/// Checks if a string matches a wildcard pattern using * (any sequence) and ? (single character)
	/// </summary>
	public static bool MatchesWildcard(string input, string pattern)
	{
		var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
		return Regex.IsMatch(input, regexPattern);
	}

	/// <summary>
	/// Counts how many times a substring (pattern) appears in a string
	/// </summary>
	public static int CountSubstringMatches(string input, string pattern)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
		{
			return 0;
		}

		return Regex.Count(input, Regex.Escape(pattern));
	}

	/// <summary>
	/// Validates if a string is a valid IPv4 address (e.g. "192.168.1.1")
	/// </summary>
	public static bool IsValidIpAddress(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		return Regex.IsMatch(input,
			@"^((25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)\.){3}(25[0-5]|2[0-4]\d|1\d{2}|[1-9]\d|\d)$");
	}

	/// <summary>
	/// Validates if a string is a valid hex color code (e.g. "#FF5733" or "#f3a")
	/// </summary>
	public static bool IsHexColor(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		return Regex.IsMatch(input, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
	}

	/// <summary>
	/// Checks if a string is a valid identifier (starts with letter or underscore, followed by letters, digits, or underscores)
	/// </summary>
	public static bool IsValidIdentifier(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		return Regex.IsMatch(input, @"^[a-zA-Z_]\w*$");
	}

	/// <summary>
	/// Extracts all consecutive digit sequences from a string and returns their count
	/// </summary>
	public static int CountDigitGroups(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return 0;
		}

		return Regex.Count(input, @"\d+");
	}

	/// <summary>
	/// Extracts all digits from a string and returns them as a new string
	/// </summary>
	public static string ExtractDigits(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}

		return Regex.Replace(input, @"\D", string.Empty);
	}

	/// <summary>
	/// Checks if a string has a repeating block of a given length at any position
	/// </summary>
	public static bool HasRepeatingBlock(string input, int blockLength)
	{
		if (string.IsNullOrEmpty(input) || blockLength <= 0 || input.Length < blockLength * 2)
		{
			return false;
		}

		return Regex.IsMatch(input, $@"(.{{{blockLength}}})\1");
	}

	/// <summary>
	/// Checks if a string matches a simple character-class pattern.
	/// Supported classes: \d (digit), \w (word char), \s (whitespace), . (any), literal chars.
	/// Each pattern character matches exactly one input character (no quantifiers).
	/// </summary>
	public static bool MatchesSimplePattern(string input, string pattern)
	{
		if (string.IsNullOrEmpty(pattern))
		{
			return string.IsNullOrEmpty(input);
		}

		return Regex.IsMatch(input, $"^{pattern}$");
	}

	/// <summary>
	/// Returns the index of the first match of pattern in input, or -1 if not found
	/// </summary>
	public static int IndexOfPattern(string input, string pattern)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
		{
			return -1;
		}

		var match = Regex.Match(input, Regex.Escape(pattern));
		return match.Success ? match.Index : -1;
	}

	/// <summary>
	/// Replaces all occurrences of a literal pattern with a replacement string
	/// </summary>
	public static string ReplaceAll(string input, string pattern, string replacement)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
		{
			return input;
		}

		return Regex.Replace(input, Regex.Escape(pattern), replacement);
	}

	/// <summary>
	/// Checks if a string starts with a given prefix (case-sensitive)
	/// </summary>
	public static bool StartsWith(string input, string prefix)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(prefix))
		{
			return false;
		}

		return Regex.IsMatch(input, $"^{Regex.Escape(prefix)}");
	}

	/// <summary>
	/// Checks if a string ends with a given suffix (case-sensitive)
	/// </summary>
	public static bool EndsWith(string input, string suffix)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(suffix))
		{
			return false;
		}

		return Regex.IsMatch(input, $"{Regex.Escape(suffix)}$");
	}

	/// <summary>
	/// Splits a string by a single-character delimiter and returns the number of parts
	/// </summary>
	public static int CountSplit(string input, char delimiter)
	{
		if (string.IsNullOrEmpty(input))
		{
			return 0;
		}

		return Regex.Split(input, Regex.Escape(delimiter.ToString())).Length;
	}
}
