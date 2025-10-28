using System;
using System.Numerics;

namespace ConstExpr.SourceGenerator.Sample;

/// <summary>
/// Provides mathematical operations for integer types
/// </summary>
public static class IntegerMath
{
	/// <summary>
	/// Computes the integer square root of a value using Newton's method (digit-by-digit calculation)
	/// </summary>
	/// <typeparam name="T">The integer type implementing IBinaryInteger</typeparam>
	/// <param name="value">The value to compute the square root of</param>
	/// <returns>The integer square root (floor of the exact square root)</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
	public static T Sqrt<T>(T value) where T : IBinaryInteger<T>
	{
		if (T.IsNegative(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), "Cannot compute square root of a negative number");
		}

		if (T.IsZero(value) || value == T.One)
		{
			return value;
		}

		// Use Newton's method for integer square root
		// x_{n+1} = (x_n + value/x_n) / 2
		
		// Start with a good initial guess based on bit length
		var x = value;
		var y = (value >> 1) + T.One; // Initial guess: (value / 2) + 1

		while (y < x)
		{
			x = y;
			y = (x + value / x) >> 1; // (x + value/x) / 2
		}

		return x;
	}

	/// <summary>
	/// Computes the integer square root using a digit-by-digit algorithm (similar to long division)
	/// This can be faster for smaller numbers
	/// </summary>
	public static T SqrtDigitByDigit<T>(T value) where T : IBinaryInteger<T>
	{
		if (T.IsNegative(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), "Cannot compute square root of a negative number");
		}

		if (T.IsZero(value) || value == T.One)
		{
			return value;
		}

		// Find the highest set bit
		var bitLength = 0;
		var temp = value;

		while (temp > T.Zero)
		{
			temp >>= 1;
			bitLength++;
		}

		// Start from the highest pair of bits
		var shift = ((bitLength + 1) / 2) * 2 - 2;
		var result = T.Zero;

		while (shift >= 0)
		{
			result <<= 1;
			var largerResult = result + T.One;

			if (largerResult * largerResult <= (value >> shift))
			{
				result = largerResult;
			}

			shift -= 2;
		}

		return result;
	}
}

