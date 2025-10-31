using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class MathematicalOperations
{
	public static double Average(params IReadOnlyList<double> data)
	{
		return data.Average();
	}

	public static double StdDev(params IReadOnlyList<double> data)
	{
		var sum = 0d;
		var sumOfSquares = 0d;

		foreach (var item in data)
		{
			sum += item;
			sumOfSquares += item * item;
		}

		var mean = sum / data.Count;
		var variance = sumOfSquares / data.Count - mean * mean;

		return Math.Sqrt(variance);
	}

	public static bool IsPrime(int number)
	{
		switch (number)
		{
			case < 2:
				return false;
			case 2:
				return true;
		}

		if (number % 2 == 0)
		{
			return false;
		}

		var sqrt = (int)Math.Sqrt(number);

		for (var i = 3; i <= sqrt; i += 2)
		{
			if (number % i == 0)
			{
				return false;
			}
		}

		return true;
	}

	public static IEnumerable<int> PrimesUpTo(int max)
	{
		if (max < 2)
		{
			yield break;
		}

		var sieve = new System.Collections.BitArray(max + 1);

		for (var p = 2; p * p <= max; p++)
		{
			if (!sieve[p])
			{
				for (var m = p * p; m <= max; m += p)
				{
					sieve[m] = true;
				}
			}
		}

		for (var i = 2; i <= max; i++)
		{
			if (!sieve[i])
			{
				yield return i;
			}
		}
	}

	public static IEnumerable<long> FibonacciSequence(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		var a = 0L;
		var b = 1L;

		for (var i = 0; i < count; i++)
		{
			yield return a;

			checked
			{
				var next = a + b;
				a = b;
				b = next;
			}
		}
	}

	public static int Clamp(int value, int min, int max)
	{
		if (min > max)
		{
			throw new ArgumentException("min cannot be greater than max");
		}

		if (value < min)
		{
			return min;
		}

		if (value > max)
		{
			return max;
		}

		return value;
	}

	public static double Map(double value, double inMin, double inMax, double outMin, double outMax)
	{
		if (Math.Abs(inMax - inMin) < double.Epsilon)
		{
			throw new ArgumentException("Input range cannot be zero", nameof(inMax));
		}

		var t = (value - inMin) / (inMax - inMin);
		return outMin + t * (outMax - outMin);
	}

	public static double PolynomialEvaluate(double x, double a, double b, double c, double d)
	{
		return a * x * x * x + b * x * x + c * x + d;
	}

	public static double WeightedAverage(double value1, double weight1, double value2, double weight2, double value3, double weight3)
	{
		var totalWeight = weight1 + weight2 + weight3;

		if (Math.Abs(totalWeight) < double.Epsilon)
		{
			throw new ArgumentException("Total weight cannot be zero");
		}

		return (value1 * weight1 + value2 * weight2 + value3 * weight3) / totalWeight;
	}

	// Additional mathematical functions
	public static double Lerp(double a, double b, double t)
	{
		return a + (b - a) * t;
	}

	public static double InverseLerp(double a, double b, double value)
	{
		if (Math.Abs(b - a) < double.Epsilon)
		{
			throw new ArgumentException("a and b cannot be equal");
		}

		return (value - a) / (b - a);
	}

	public static double Median(params IReadOnlyList<double> data)
	{
		if (data.Count == 0)
		{
			throw new ArgumentException("Cannot calculate median of empty collection");
		}

		var sorted = data.OrderBy(x => x).ToArray();
		var mid = sorted.Length / 2;

		if (sorted.Length % 2 == 0)
		{
			return (sorted[mid - 1] + sorted[mid]) / 2.0;
		}

		return sorted[mid];
	}

	public static int GreatestCommonDivisor(int a, int b)
	{
		a = Math.Abs(a);
		b = Math.Abs(b);

		while (b != 0)
		{
			var temp = b;
			b = a % b;
			a = temp;
		}

		return a;
	}

	public static int LeastCommonMultiple(int a, int b)
	{
		if (a == 0 || b == 0)
		{
			return 0;
		}

		return Math.Abs(a * b) / GreatestCommonDivisor(a, b);
	}

	public static long Factorial(int n)
	{
		if (n < 0)
		{
			throw new ArgumentException("Factorial is not defined for negative numbers");
		}

		if (n == 0 || n == 1)
		{
			return 1;
		}

		var result = 1L;

		for (var i = 2; i <= n; i++)
		{
			result *= i;
		}

		return result;
	}

	public static long Combination(int n, int k)
	{
		if (k < 0 || k > n)
		{
			return 0;
		}

		if (k == 0 || k == n)
		{
			return 1;
		}

		k = Math.Min(k, n - k);
		var result = 1L;

		for (var i = 0; i < k; i++)
		{
			result = result * (n - i) / (i + 1);
		}

		return result;
	}

	public static double Power(double baseValue, int exponent)
	{
		if (exponent == 0)
		{
			return 1;
		}

		if (exponent < 0)
		{
			return 1.0 / Power(baseValue, -exponent);
		}

		var result = 1.0;

		for (var i = 0; i < exponent; i++)
		{
			result *= baseValue;
		}

		return result;
	}

	public static bool IsPerfectSquare(int n)
	{
		if (n < 0)
		{
			return false;
		}

		var sqrt = (int)Math.Sqrt(n);
		return sqrt * sqrt == n;
	}

	public static double RoundToDecimalPlaces(double value, int decimalPlaces)
	{
		if (decimalPlaces < 0)
		{
			throw new ArgumentException("Decimal places cannot be negative");
		}

		return Math.Round(value, decimalPlaces);
	}
}

