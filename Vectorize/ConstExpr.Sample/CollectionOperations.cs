using ConstExpr.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class CollectionOperations
{
	public static IEnumerable<double> IsOdd(params IEnumerable<double> data)
	{
		return data.Where(w => w % 2 != 0);
	}

	public static int[] GetArray(params int[] items)
	{
		return items[1..5];
	}

	public static IList<byte> Range(int count)
	{
		var random = new Random();
		var result = new List<byte>(count);

		for (var i = 0; i < count; i++)
		{
			result.Add((byte)random.Next(i));
		}

		return result;
	}

	public static int[] FilterAndTransform(IEnumerable<int> array, int minValue, int maxValue, int multiplier)
	{
		return array
			.Where(x => x >= minValue && x <= maxValue)
			.Select(x => x * multiplier)
			.ToArray();
	}

	// Additional collection operations
	public static T[] ReverseArray<T>(params T[] array)
	{
		if (array.Length <= 1)
		{
			return array;
		}

		var result = new T[array.Length];

		for (var i = 0; i < array.Length; i++)
		{
			result[i] = array[array.Length - 1 - i];
		}

		return result;
	}

	public static T[] ChunkArray<T>(T[] array, int chunkSize, int chunkIndex)
	{
		if (array == null || chunkSize <= 0 || chunkIndex < 0)
		{
			throw new ArgumentException("Invalid parameters");
		}

		var startIndex = chunkIndex * chunkSize;

		if (startIndex >= array.Length)
		{
			return Array.Empty<T>();
		}

		var length = Math.Min(chunkSize, array.Length - startIndex);
		var result = new T[length];

		Array.Copy(array, startIndex, result, 0, length);

		return result;
	}

	public static int[] GenerateRange(int start, int count, int step = 1)
	{
		if (count < 0)
		{
			throw new ArgumentException("Count cannot be negative");
		}

		var result = new int[count];

		for (var i = 0; i < count; i++)
		{
			result[i] = start + (i * step);
		}

		return result;
	}

	public static T[] RemoveDuplicates<T>(params T[] array) where T : IEquatable<T>
	{
		if (array == null || array.Length == 0)
		{
			return array;
		}

		var result = new List<T>();

		foreach (var item in array)
		{
			if (!result.Contains(item))
			{
				result.Add(item);
			}
		}

		return result.ToArray();
	}

	public static int[] IntersectArrays(int[] array1, int[] array2)
	{
		if (array1 == null || array2 == null)
		{
			return Array.Empty<int>();
		}

		return array1.Intersect(array2).ToArray();
	}

	public static int[] UnionArrays(int[] array1, int[] array2)
	{
		if (array1 == null && array2 == null)
		{
			return Array.Empty<int>();
		}

		if (array1 == null)
		{
			return array2;
		}

		if (array2 == null)
		{
			return array1;
		}

		return array1.Union(array2).ToArray();
	}

	public static T[] FlattenNestedArray<T>(T[][] nestedArray)
	{
		if (nestedArray == null)
		{
			return Array.Empty<T>();
		}

		var result = new List<T>();

		foreach (var subArray in nestedArray)
		{
			if (subArray != null)
			{
				result.AddRange(subArray);
			}
		}

		return result.ToArray();
	}

	public static Dictionary<T, int> CountOccurrences<T>(params T[] array) where T : notnull
	{
		var result = new Dictionary<T, int>();

		foreach (var item in array)
		{
			if (result.ContainsKey(item))
			{
				result[item]++;
			}
			else
			{
				result[item] = 1;
			}
		}

		return result;
	}

	public static int[] RotateArray(int[] array, int positions)
	{
		if (array == null || array.Length == 0)
		{
			return array;
		}

		positions = positions % array.Length;

		if (positions < 0)
		{
			positions += array.Length;
		}

		var result = new int[array.Length];

		for (var i = 0; i < array.Length; i++)
		{
			result[i] = array[(i + positions) % array.Length];
		}

		return result;
	}

	public static T FindMax<T>(params T[] array) where T : IComparable<T>
	{
		if (array == null || array.Length == 0)
		{
			throw new ArgumentException("Array cannot be null or empty");
		}

		var max = array[0];

		for (var i = 1; i < array.Length; i++)
		{
			if (array[i].CompareTo(max) > 0)
			{
				max = array[i];
			}
		}

		return max;
	}

	public static T FindMin<T>(params T[] array) where T : IComparable<T>
	{
		if (array == null || array.Length == 0)
		{
			throw new ArgumentException("Array cannot be null or empty");
		}

		var min = array[0];

		for (var i = 1; i < array.Length; i++)
		{
			if (array[i].CompareTo(min) < 0)
			{
				min = array[i];
			}
		}

		return min;
	}

	public static int[] BubbleSort(params int[] array)
	{
		if (array.Length <= 1)
		{
			return array;
		}

		var result = new int[array.Length];
		Array.Copy(array, result, array.Length);

		for (var i = 0; i < result.Length - 1; i++)
		{
			for (var j = 0; j < result.Length - i - 1; j++)
			{
				if (result[j] > result[j + 1])
				{
					(result[j], result[j + 1]) = (result[j + 1], result[j]);
				}
			}
		}

		return result;
	}

	public static int BinarySearch(int[] sortedArray, int target)
	{
		if (sortedArray == null || sortedArray.Length == 0)
		{
			return -1;
		}

		var left = 0;
		var right = sortedArray.Length - 1;

		while (left <= right)
		{
			var mid = left + (right - left) / 2;

			if (sortedArray[mid] == target)
			{
				return mid;
			}

			if (sortedArray[mid] < target)
			{
				left = mid + 1;
			}
			else
			{
				right = mid - 1;
			}
		}

		return -1;
	}
}

