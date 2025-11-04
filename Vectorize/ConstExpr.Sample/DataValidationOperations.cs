using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class DataValidationOperations
{
	/// <summary>
	/// Validates if a number is within a specified range
	/// </summary>
	public static bool IsInRange(double value, double min, double max)
	{
		return value >= min && value <= max;
	}

	/// <summary>
	/// Validates if all numbers in a collection are positive
	/// </summary>
	public static bool AllPositive(params double[] numbers)
	{
		return numbers.All(n => n > 0);
	}

	/// <summary>
	/// Validates if a string matches a simple pattern (only alphanumeric)
	/// </summary>
	public static bool IsAlphanumeric(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return false;
		}

		return input.All(c => char.IsLetterOrDigit(c));
	}

	/// <summary>
	/// Validates email format with basic rules
	/// </summary>
	public static bool IsValidEmail(string email)
	{
		if (string.IsNullOrEmpty(email) || email.Length < 5)
		{
			return false;
		}

		var atCount = 0;
		var dotCount = 0;
		var atIndex = -1;
		var lastDotIndex = -1;

		for (var i = 0; i < email.Length; i++)
		{
			if (email[i] == '@')
			{
				atCount++;
				atIndex = i;
			}
			else if (email[i] == '.')
			{
				dotCount++;
				lastDotIndex = i;
			}
		}

		// Must have exactly one @ and at least one .
		// @ must not be at start or end
		// . must be after @ and not at end
		return atCount == 1 && dotCount >= 1 && atIndex > 0 && atIndex < email.Length - 1 &&
		       lastDotIndex > atIndex + 1 && lastDotIndex < email.Length - 1;
	}

	/// <summary>
	/// Validates if a string is a valid phone number (digits and hyphens only)
	/// </summary>
	public static bool IsValidPhoneNumber(string phone)
	{
		if (string.IsNullOrEmpty(phone) || phone.Length < 10)
		{
			return false;
		}

		var digitCount = 0;
		foreach (var c in phone)
		{
			if (char.IsDigit(c))
			{
				digitCount++;
			}
			else if (c != '-' && c != ' ' && c != '+')
			{
				return false;
			}
		}

		return digitCount >= 10 && digitCount <= 15;
	}

	/// <summary>
	/// Calculates data quality score based on non-null entries
	/// </summary>
	public static double CalculateDataQuality(params double[] values)
	{
		if (values.Length == 0)
		{
			return 0.0;
		}

		var nonNullCount = values.Count(v => !double.IsNaN(v) && !double.IsInfinity(v));
		return (double)nonNullCount / values.Length;
	}

	/// <summary>
	/// Detects outliers using standard deviation
	/// </summary>
	public static int[] DetectOutliers(double[] data, double standardDeviations = 2.0)
	{
		if (data.Length < 2)
		{
			return Array.Empty<int>();
		}

		var mean = data.Average();
		var variance = data.Average(x => Math.Pow(x - mean, 2));
		var stdDev = Math.Sqrt(variance);
		var threshold = stdDev * standardDeviations;

		var outliers = new List<int>();
		for (var i = 0; i < data.Length; i++)
		{
			if (Math.Abs(data[i] - mean) > threshold)
			{
				outliers.Add(i);
			}
		}

		return outliers.ToArray();
	}

	/// <summary>
	/// Validates if a collection has balanced parentheses
	/// </summary>
	public static bool HasBalancedParentheses(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return true;
		}

		var balance = 0;
		foreach (var c in input)
		{
			if (c == '(')
			{
				balance++;
			}
			else if (c == ')')
			{
				balance--;
				if (balance < 0)
				{
					return false;
				}
			}
		}

		return balance == 0;
	}

	/// <summary>
	/// Validates credit card number using Luhn algorithm (simplified)
	/// </summary>
	public static bool IsValidCreditCard(string cardNumber)
	{
		if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 13 || cardNumber.Length > 19)
		{
			return false;
		}

		return ValidateLuhnChecksum(cardNumber);
	}

	private static bool ValidateLuhnChecksum(string cardNumber)
	{
		var sum = 0;
		var isSecond = false;

		for (var i = cardNumber.Length - 1; i >= 0; i--)
		{
			if (!char.IsDigit(cardNumber[i]))
			{
				return false;
			}

			var digit = cardNumber[i] - '0';

			if (isSecond)
			{
				digit *= 2;
				if (digit > 9)
				{
					digit -= 9;
				}
			}

			sum += digit;
			isSecond = !isSecond;
		}

		return sum % 10 == 0;
	}
}

