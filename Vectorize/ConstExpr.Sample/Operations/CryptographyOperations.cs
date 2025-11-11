using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Sample.Operations;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class CryptographyOperations
{
	/// <summary>
	/// Calculates a simple checksum using XOR operations for input validation
	/// </summary>
	public static byte CalculateChecksum(params byte[] data)
	{
		var checksum = (byte)0;
		foreach (var b in data)
		{
			checksum ^= b;
		}
		return checksum;
	}

	/// <summary>
	/// Simple Caesar cipher encryption with variable shift
	/// </summary>
	public static string CaesarEncrypt(string text, int shift)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		var result = new System.Text.StringBuilder();
		var normalizedShift = ((shift % 26) + 26) % 26;

		foreach (var c in text)
		{
			if (char.IsLetter(c))
			{
				var baseChar = char.IsUpper(c) ? 'A' : 'a';
				var charIndex = c - baseChar;
				var newIndex = (charIndex + normalizedShift) % 26;
				result.Append((char)(baseChar + newIndex));
			}
			else
			{
				result.Append(c);
			}
		}

		return result.ToString();
	}

	/// <summary>
	/// Simple Caesar cipher decryption
	/// </summary>
	public static string CaesarDecrypt(string text, int shift)
	{
		return CaesarEncrypt(text, -shift);
	}

	/// <summary>
	/// Validates if a string contains valid hex characters
	/// </summary>
	public static bool IsValidHex(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		return input.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
	}

	/// <summary>
	/// Converts a byte array to hexadecimal string representation
	/// </summary>
	public static string BytesToHex(params byte[] data)
	{
		if (data.Length == 0)
		{
			return string.Empty;
		}

		var result = new System.Text.StringBuilder(data.Length * 2);

		foreach (var b in data)
		{
			result.Append(b.ToString("X2"));
		}

		return result.ToString();
	}

	/// <summary>
	/// Calculates a simple hash using polynomial rolling hash
	/// </summary>
	public static ulong PolynomialHash(string input, int prime = 31)
	{
		if (string.IsNullOrEmpty(input))
		{
			return 0;
		}

		const ulong mod = 1000000007;
		var hash = 0UL;
		var primePower = 1UL;

		for (var i = input.Length - 1; i >= 0; i--)
		{
			hash = (hash + input[i] * primePower) % mod;
			primePower = (primePower * (ulong)prime) % mod;
		}

		return hash;
	}
}

