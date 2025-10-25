using ConstExpr.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class MiscellaneousOperations
{
	public static string ToString<T>(this T value) where T : Enum
	{
		return value.ToString();
	}

	public static IEnumerable<string> GetNames<T>() where T : struct, Enum
	{
		return Enum.GetNames<T>();
	}

	public async static Task<string> Waiting()
	{
		// await Task.Delay(1000);

		return "MiscellaneousOperations";
	}

	public static string DetermineGrade(double score, double maxScore, bool useCurve, double curveBonus)
	{
		if (maxScore <= 0)
		{
			throw new ArgumentException("Max score must be positive");
		}

		var percentage = (score / maxScore) * 100.0;

		if (useCurve)
		{
			percentage += curveBonus;
		}

		percentage = Math.Min(percentage, 100.0);

		return percentage switch
		{
			>= 90.0 => "A",
			>= 80.0 => "B",
			>= 70.0 => "C",
			>= 60.0 => "D",
			_ => "F"
		};
	}

	// Additional miscellaneous operations
	public static bool IsEven(int number)
	{
		return number % 2 == 0;
	}

	public static bool IsOddInt(int number)
	{
		return number % 2 != 0;
	}

	public static int AbsoluteValue(int value)
	{
		return value < 0 ? -value : value;
	}

	public static double AbsoluteValueDouble(double value)
	{
		return value < 0 ? -value : value;
	}

	public static int Sign(double value)
	{
		if (value > 0) return 1;
		if (value < 0) return -1;
		return 0;
	}

	public static int RandomInRange(int min, int max)
	{
		var random = new Random();
		return random.Next(min, max + 1);
	}

	public static bool IsInRange(int value, int min, int max)
	{
		return value >= min && value <= max;
	}

	public static double Percentage(double value, double total)
	{
		if (total == 0)
		{
			throw new ArgumentException("Total cannot be zero");
		}

		return (value / total) * 100.0;
	}

	public static double PercentageOf(double percentage, double total)
	{
		return (percentage / 100.0) * total;
	}

	public static double PercentageIncrease(double oldValue, double newValue)
	{
		if (oldValue == 0)
		{
			throw new ArgumentException("Old value cannot be zero");
		}

		return ((newValue - oldValue) / oldValue) * 100.0;
	}

	public static double PercentageDecrease(double oldValue, double newValue)
	{
		if (oldValue == 0)
		{
			throw new ArgumentException("Old value cannot be zero");
		}

		return ((oldValue - newValue) / oldValue) * 100.0;
	}

	public static int DivideAndRoundUp(int dividend, int divisor)
	{
		if (divisor == 0)
		{
			throw new ArgumentException("Divisor cannot be zero");
		}

		return (dividend + divisor - 1) / divisor;
	}

	public static bool IsPowerOfTwo(int n)
	{
		return n > 0 && (n & (n - 1)) == 0;
	}

	public static int NextPowerOfTwo(int n)
	{
		if (n < 1)
		{
			return 1;
		}

		n--;
		n |= n >> 1;
		n |= n >> 2;
		n |= n >> 4;
		n |= n >> 8;
		n |= n >> 16;
		return n + 1;
	}

	public static int CountBits(int n)
	{
		var count = 0;

		while (n != 0)
		{
			count += n & 1;
			n >>= 1;
		}

		return count;
	}

	public static int ReverseBits(int n)
	{
		var result = 0;

		for (var i = 0; i < 32; i++)
		{
			result = (result << 1) | (n & 1);
			n >>= 1;
		}

		return result;
	}

	public static bool IsBitSet(int number, int position)
	{
		if (position < 0 || position >= 32)
		{
			throw new ArgumentException("Position must be between 0 and 31");
		}

		return (number & (1 << position)) != 0;
	}

	public static int SetBit(int number, int position)
	{
		if (position < 0 || position >= 32)
		{
			throw new ArgumentException("Position must be between 0 and 31");
		}

		return number | (1 << position);
	}

	public static int ClearBit(int number, int position)
	{
		if (position < 0 || position >= 32)
		{
			throw new ArgumentException("Position must be between 0 and 31");
		}

		return number & ~(1 << position);
	}

	public static int ToggleBit(int number, int position)
	{
		if (position < 0 || position >= 32)
		{
			throw new ArgumentException("Position must be between 0 and 31");
		}

		return number ^ (1 << position);
	}

	public static string DecimalToBinary(int number)
	{
		if (number == 0)
		{
			return "0";
		}

		var result = string.Empty;
		var n = number;

		while (n > 0)
		{
			result = (n % 2) + result;
			n /= 2;
		}

		return result;
	}

	public static int BinaryToDecimal(string binary)
	{
		if (string.IsNullOrEmpty(binary))
		{
			throw new ArgumentException("Binary string cannot be null or empty");
		}

		var result = 0;
		var power = 0;

		for (var i = binary.Length - 1; i >= 0; i--)
		{
			if (binary[i] == '1')
			{
				result += 1 << power;
			}
			else if (binary[i] != '0')
			{
				throw new ArgumentException("Invalid binary string");
			}

			power++;
		}

		return result;
	}

	public static string DecimalToHex(int number)
	{
		if (number == 0)
		{
			return "0";
		}

		const string hexDigits = "0123456789ABCDEF";
		var result = string.Empty;
		var n = number;

		while (n > 0)
		{
			result = hexDigits[n % 16] + result;
			n /= 16;
		}

		return result;
	}

	public static int HexToDecimal(string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			throw new ArgumentException("Hex string cannot be null or empty");
		}

		var result = 0;

		foreach (var c in hex.ToUpperInvariant())
		{
			result *= 16;

			if (c >= '0' && c <= '9')
			{
				result += c - '0';
			}
			else if (c >= 'A' && c <= 'F')
			{
				result += c - 'A' + 10;
			}
			else
			{
				throw new ArgumentException("Invalid hex string");
			}
		}

		return result;
	}
}

