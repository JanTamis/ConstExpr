using System;
using System.Text;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.SourceGenerator.Sample.Operations;

[ConstExpr(
	MathOptimizations = FastMathFlags.All,
	Optimizations = OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination,
	LinqOptimization = LinqOptimizationMode.Unroll)]
public static class StringOperations
{
	/// <summary>
	///   Reverses a string
	/// </summary>
	public static string Reverse(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return input;
		}

		var chars = input.ToCharArray();
		var left = 0;
		var right = chars.Length - 1;

		while (left < right)
		{
			var temp = chars[left];
			chars[left] = chars[right];
			chars[right] = temp;
			left++;
			right--;
		}

		return new string(chars);
	}

	/// <summary>
	///   Checks if a string is a palindrome
	/// </summary>
	public static bool IsPalindrome(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return true;
		}

		var left = 0;
		var right = input.Length - 1;

		while (left < right)
		{
			if (Char.ToLower(input[left]) != Char.ToLower(input[right]))
			{
				return false;
			}
			left++;
			right--;
		}

		return true;
	}

	/// <summary>
	///   Counts vowels in a string
	/// </summary>
	public static int CountVowels(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return 0;
		}

		var count = 0;

		foreach (var c in input)
		{
			var lower = Char.ToLower(c);

			if (lower == 'a' || lower == 'e' || lower == 'i' || lower == 'o' || lower == 'u')
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	///   Counts consonants in a string
	/// </summary>
	public static int CountConsonants(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return 0;
		}

		var count = 0;

		foreach (var c in input)
		{
			if (Char.IsLetter(c))
			{
				var lower = Char.ToLower(c);

				if (lower != 'a' && lower != 'e' && lower != 'i' && lower != 'o' && lower != 'u')
				{
					count++;
				}
			}
		}

		return count;
	}

	/// <summary>
	///   Converts string to title case
	/// </summary>
	public static string ToTitleCase(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return input;
		}

		var result = input.ToCharArray();
		var capitalizeNext = true;

		for (var i = 0; i < result.Length; i++)
		{
			var c = result[i];

			if (Char.IsWhiteSpace(c))
			{
				capitalizeNext = true;
			}
			else if (capitalizeNext)
			{
				result[i] = Char.ToUpper(c);
				capitalizeNext = false;
			}
		}

		return new string(result);
	}

	/// <summary>
	///   Counts occurrences of a character
	/// </summary>
	public static int CountChar(string input, char target)
	{
		if (String.IsNullOrEmpty(input))
		{
			return 0;
		}

		var count = 0;

		foreach (var c in input)
		{
			if (c == target)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	///   Removes whitespace from string
	/// </summary>
	public static string RemoveWhitespace(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return input;
		}

		var result = new char[input.Length];
		var index = 0;

		foreach (var c in input)
		{
			if (!Char.IsWhiteSpace(c))
			{
				result[index++] = c;
			}
		}

		return new string(result, 0, index);
	}

	/// <summary>
	///   Checks if string contains only digits
	/// </summary>
	public static bool IsNumeric(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return false;
		}

		foreach (var c in input)
		{
			if (!Char.IsDigit(c))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Counts words in a string
	/// </summary>
	public static int CountWords(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return 0;
		}

		var count = 0;
		var inWord = false;

		foreach (var c in input)
		{
			if (Char.IsWhiteSpace(c))
			{
				inWord = false;
			}
			else if (!inWord)
			{
				inWord = true;
				count++;
			}
		}

		return count;
	}

	/// <summary>
	///   Repeats a string n times
	/// </summary>
	public static string Repeat(string input, int count)
	{
		if (String.IsNullOrEmpty(input) || count <= 0)
		{
			return String.Empty;
		}

		var result = new StringBuilder(input.Length * count);

		for (var i = 0; i < count; i++)
		{
			result.Append(input);
		}

		return result.ToString();
	}

	/// <summary>
	///   Converts string to alternating case
	/// </summary>
	public static string ToAlternatingCase(string input)
	{
		if (String.IsNullOrEmpty(input))
		{
			return input;
		}

		var result = input.ToCharArray();

		for (var i = 0; i < result.Length; i++)
		{
			var c = result[i];

			if (Char.IsUpper(c))
			{
				result[i] = Char.ToLower(c);
			}
			else if (Char.IsLower(c))
			{
				result[i] = Char.ToUpper(c);
			}
		}

		return new string(result);
	}

	/// <summary>
	///   Checks if strings are anagrams
	/// </summary>
	public static bool AreAnagrams(string? str1, string? str2)
	{
		if (str1 == null || str2 == null)
		{
			return false;
		}

		// Remove whitespace and convert to lowercase
		var s1 = RemoveWhitespace(str1.ToLower());
		var s2 = RemoveWhitespace(str2.ToLower());

		if (s1.Length != s2.Length)
		{
			return false;
		}

		// Count character frequencies
		var charCount = new int[256];

		foreach (var c in s1)
		{
			charCount[c]++;
		}

		foreach (var c in s2)
		{
			charCount[c]--;
		}

		for (var i = 0; i < 256; i++)
		{
			if (charCount[i] != 0)
			{
				return false;
			}
		}

		return true;
	}
}