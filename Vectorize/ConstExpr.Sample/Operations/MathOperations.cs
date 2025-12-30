using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.SourceGenerator.Sample.Operations;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class MathOperations
{
	/// <summary>
	/// Calculates factorial of a number
	/// </summary>
	public static long Factorial(int n)
	{
		if (n < 0)
		{
			return -1;
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

	/// <summary>
	/// Calculates the nth Fibonacci number
	/// </summary>
	public static long Fibonacci(int n)
	{
		if (n <= 0)
		{
			return 0;
		}

		if (n == 1)
		{
			return 1;
		}

		var prev = 0L;
		var curr = 1L;

		for (var i = 2; i <= n; i++)
		{
			var next = prev + curr;
			prev = curr;
			curr = next;
		}

		return curr;
	}

	/// <summary>
	/// Checks if a number is prime
	/// </summary>
	public static bool IsPrime(int n)
	{
		if (n <= 1)
		{
			return false;
		}

		if (n <= 3)
		{
			return true;
		}

		if (n % 2 == 0 || n % 3 == 0)
		{
			return false;
		}

		for (var i = 5; i * i <= n; i += 6)
		{
			if (n % i == 0 || n % (i + 2) == 0)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Calculates the Greatest Common Divisor using Euclidean algorithm
	/// </summary>
	public static int GCD(int a, int b)
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

	/// <summary>
	/// Calculates the Least Common Multiple
	/// </summary>
	public static int LCM(int a, int b)
	{
		if (a == 0 || b == 0)
		{
			return 0;
		}

		return Math.Abs(a * b) / GCD(a, b);
	}

	/// <summary>
	/// Calculates power using fast exponentiation
	/// </summary>
	public static long Power(int baseNum, int exponent)
	{
		if (exponent < 0)
		{
			return 0;
		}

		if (exponent == 0)
		{
			return 1;
		}

		var result = 1L;
		var base64 = (long) baseNum;

		while (exponent > 0)
		{
			if (exponent % 2 == 1)
			{
				result *= base64;
			}
			base64 *= base64;
			exponent /= 2;
		}

		return result;
	}

	/// <summary>
	/// Calculates sum of digits
	/// </summary>
	public static int SumOfDigits(int n)
	{
		n = Math.Abs(n);
		var sum = 0;

		while (n > 0)
		{
			sum += n % 10;
			n /= 10;
		}

		return sum;
	}

	/// <summary>
	/// Checks if a number is a perfect square
	/// </summary>
	public static bool IsPerfectSquare(int n)
	{
		if (n < 0)
		{
			return false;
		}

		var sqrt = (int) Math.Sqrt(n);
		return sqrt * sqrt == n;
	}

	/// <summary>
	/// Calculates the number of digits in a number
	/// </summary>
	public static int CountDigits(int n)
	{
		if (n == 0)
		{
			return 1;
		}

		n = Math.Abs(n);
		var count = 0;

		while (n > 0)
		{
			count++;
			n /= 10;
		}

		return count;
	}

	/// <summary>
	/// Reverses the digits of a number
	/// </summary>
	public static int ReverseNumber(int n)
	{
		var originalN = n;
		n = Math.Abs(n);

		var reversed = 0;

		while (n > 0)
		{
			reversed = reversed * 10 + n % 10;
			n /= 10;
		}

		return Int32.CopySign(reversed, originalN);
	}

	/// <summary>
	/// Calculates the sum of numbers in a range
	/// </summary>
	public static long SumRange(int start, int end)
	{
		if (start > end)
		{
			var temp = start;
			start = end;
			end = temp;
		}

		var n = end - start + 1;
		return (long) n * (start + end) / 2;
	}

	/// <summary>
	/// Calculates binomial coefficient (n choose k)
	/// </summary>
	public static long BinomialCoefficient(int n, int k)
	{
		if (k < 0 || k > n)
		{
			return 0;
		}

		if (k == 0 || k == n)
		{
			return 1;
		}

		// Optimize by using smaller k
		if (k > n - k)
		{
			k = n - k;
		}

		var result = 1L;

		for (var i = 0; i < k; i++)
		{
			result = result * (n - i) / (i + 1);
		}

		return result;
	}
}