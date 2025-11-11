using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;
using System.Linq;

namespace ConstExpr.SourceGenerator.Sample.Operations;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class ArrayOperations
{
	/// <summary>
	/// Finds the maximum value in an array
	/// </summary>
	public static int FindMax(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0;
		}

		var max = numbers[0];
		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] > max)
			{
				max = numbers[i];
			}
		}

		return max;
	}

	/// <summary>
	/// Finds the minimum value in an array
	/// </summary>
	public static int FindMin(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0;
		}

		var min = numbers[0];
		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] < min)
			{
				min = numbers[i];
			}
		}

		return min;
	}

	/// <summary>
	/// Calculates the average of numbers
	/// </summary>
	public static double Average(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0.0;
		}

		var sum = 0;
		foreach (var num in numbers)
		{
			sum += num;
		}

		return (double)sum / numbers.Length;
	}

	/// <summary>
	/// Calculates the median of numbers
	/// </summary>
	public static double Median(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0.0;
		}

		// Simple bubble sort for small arrays
		var sorted = new int[numbers.Length];
		Array.Copy(numbers, sorted, numbers.Length);

		for (var i = 0; i < sorted.Length - 1; i++)
		{
			for (var j = 0; j < sorted.Length - i - 1; j++)
			{
				if (sorted[j] > sorted[j + 1])
				{
					var temp = sorted[j];
					sorted[j] = sorted[j + 1];
					sorted[j + 1] = temp;
				}
			}
		}

		if (sorted.Length % 2 == 0)
		{
			return (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;
		}
		else
		{
			return sorted[sorted.Length / 2];
		}
	}

	/// <summary>
	/// Checks if array is sorted in ascending order
	/// </summary>
	public static bool IsSorted(params int[] numbers)
	{
		if (numbers.Length <= 1)
		{
			return true;
		}

		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] < numbers[i - 1])
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Removes duplicates from array
	/// </summary>
	public static int[] RemoveDuplicates(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return Array.Empty<int>();
		}

		var unique = new System.Collections.Generic.List<int>();

		foreach (var num in numbers)
		{
			var found = false;
			foreach (var existing in unique)
			{
				if (existing == num)
				{
					found = true;
					break;
				}
			}

			if (!found)
			{
				unique.Add(num);
			}
		}

		return unique.ToArray();
	}

	/// <summary>
	/// Counts occurrences of a value
	/// </summary>
	public static int CountOccurrences(int target, params int[] numbers)
	{
		var count = 0;
		foreach (var num in numbers)
		{
			if (num == target)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Finds the index of the first occurrence of a value
	/// </summary>
	public static int IndexOf(int target, params int[] numbers)
	{
		for (var i = 0; i < numbers.Length; i++)
		{
			if (numbers[i] == target)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Reverses an array
	/// </summary>
	public static int[] Reverse(params int[] numbers)
	{
		var result = new int[numbers.Length];
		for (var i = 0; i < numbers.Length; i++)
		{
			result[i] = numbers[numbers.Length - 1 - i];
		}

		return result;
	}

	/// <summary>
	/// Rotates array left by n positions
	/// </summary>
	public static int[] RotateLeft(int positions, params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return Array.Empty<int>();
		}

		positions = positions % numbers.Length;
		if (positions < 0)
		{
			positions += numbers.Length;
		}

		var result = new int[numbers.Length];
		for (var i = 0; i < numbers.Length; i++)
		{
			result[i] = numbers[(i + positions) % numbers.Length];
		}

		return result;
	}

	/// <summary>
	/// Calculates the sum of all elements
	/// </summary>
	public static int Sum(params int[] numbers)
	{
		var sum = 0;
		foreach (var num in numbers)
		{
			sum += num;
		}

		return sum;
	}

	/// <summary>
	/// Calculates the product of all elements
	/// </summary>
	public static long Product(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0;
		}

		var product = 1L;
		foreach (var num in numbers)
		{
			product *= num;
		}

		return product;
	}

	/// <summary>
	/// Finds the second largest value
	/// </summary>
	public static int SecondLargest(params int[] numbers)
	{
		if (numbers.Length < 2)
		{
			return numbers.Length == 1 ? numbers[0] : 0;
		}

		var max = int.MinValue;
		var secondMax = int.MinValue;

		foreach (var num in numbers)
		{
			if (num > max)
			{
				secondMax = max;
				max = num;
			}
			else if (num > secondMax && num != max)
			{
				secondMax = num;
			}
		}

		return secondMax;
	}

	/// <summary>
	/// Checks if array contains a specific value
	/// </summary>
	public static bool Contains(int target, params int[] numbers)
	{
		foreach (var num in numbers)
		{
			if (num == target)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Finds the range (max - min)
	/// </summary>
	public static int Range(params int[] numbers)
	{
		if (numbers.Length == 0)
		{
			return 0;
		}

		return FindMax(numbers) - FindMin(numbers);
	}
}
