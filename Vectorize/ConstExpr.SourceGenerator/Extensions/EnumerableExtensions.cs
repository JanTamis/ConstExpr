using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Extensions;

public static class EnumerableExtensions
{
	public static IEnumerable<(int Index, T Value)> Index<T>(this IEnumerable<T> source)
	{
		var index = 0;

		foreach (var item in source)
		{
			yield return (index++, item);
		}
	}

	public static object? Sum<T>(this ReadOnlySpan<T> source)
	{
		//using var enumerator = source.GetEnumerator();

		if (source.Length == 0)
		{
			return null;
		}

		var sum = source[0];

		for (int i = 1; i < source.Length; i++)
		{
			sum = Add(sum, source[i]);
		}

		return sum;
	}

	private static T Add<T>(T? a, T? b)
	{
		return (T)(object)(a switch
		{
			sbyte aSByte when b is sbyte bSByte => aSByte + bSByte,
			byte aByte when b is byte bByte => aByte + bByte,
			short aShort when b is short bShort => aShort + bShort,
			ushort aUShort when b is ushort bUShort => aUShort + bUShort,
			int ai when b is int bi => ai + bi,
			uint ai when b is uint bi => ai + bi,
			float af when b is float bf => af + bf,
			double ad when b is double bd => ad + bd,
			decimal am when b is decimal bm => am + bm,
			_ => throw new NotSupportedException($"Cannot add {a?.GetType()} and {b?.GetType()}")
		});
	}

	public static object? Average(this IEnumerable<object?> source, ITypeSymbol elementType)
	{
		return elementType.SpecialType switch
		{
			SpecialType.System_Int32 => source.Cast<int>().Average(),
			SpecialType.System_Int64 => source.Cast<long>().Average(),
			SpecialType.System_Single => source.Cast<float>().Average(),
			SpecialType.System_Double => source.Cast<double>().Average(),
			SpecialType.System_Decimal => source.Cast<decimal>().Average(),
			_ => null,
		};
	}

	public static T Average<T>(this ReadOnlySpan<T> source, ITypeSymbol elementType)
	{
		if (source.Length == 0)
		{
			return default;
		}

		return (T)(object)(elementType.SpecialType switch
		{
			SpecialType.System_Int32 => AverageInt(source),
			SpecialType.System_Int64 => AverageLong(source),
			SpecialType.System_Single => AverageFloat(source),
			SpecialType.System_Double => AverageDouble(source),
			SpecialType.System_Decimal => AverageDecimal(source),
			_ => default,
		});
	}

	private static double AverageInt<T>(ReadOnlySpan<T> source)
	{
		long sum = 0;
		var count = source.Length;

		foreach (var item in source)
		{
			if (item is int value)
			{
				sum += value;
			}
		}

		return count > 0 ? (double)sum / count : 0;
	}

	private static double AverageLong<T>(ReadOnlySpan<T> source)
	{
		long sum = 0;
		var count = source.Length;

		foreach (var item in source)
		{
			if (item is long value)
			{
				sum += value;
			}
		}

		return count > 0 ? (double)sum / count : 0;
	}

	private static float AverageFloat<T>(ReadOnlySpan<T> source)
	{
		float sum = 0;
		var count = source.Length;

		foreach (var item in source)
		{
			if (item is float value)
			{
				sum += value;
			}
		}

		return count > 0 ? sum / count : 0;
	}

	private static double AverageDouble<T>(ReadOnlySpan<T> source)
	{
		double sum = 0;
		var count = source.Length;

		foreach (var item in source)
		{
			if (item is double value)
			{
				sum += value;
			}
		}

		return count > 0 ? sum / count : 0;
	}

	private static decimal AverageDecimal<T>(ReadOnlySpan<T> source)
	{
		decimal sum = 0;
		var count = source.Length;

		foreach (var item in source)
		{
			if (item is decimal value)
			{
				sum += value;
			}
		}

		return count > 0 ? sum / count : 0;
	}

	public static IEnumerable<T> Repeat<T>(this IEnumerable<T> value, int count)
	{
		while (true)
		{
			foreach (var item in value)
			{
				if (count-- <= 0)
				{
					yield break;
				}

				yield return item;
			}
		}
	}

	public static bool IsNumericSequence<T>(this ReadOnlySpan<T> items)
	{
		if (items.Length == 0)
		{
			return true; // Empty list or single item is considered a sequence
		}

		// Check if all items are integer types
		foreach (var item in items)
		{
			if (item is not (sbyte or byte or short or ushort or int or uint or long or ulong))
			{
				return false;
			}
		}

		// Check if items form a sequence with difference of 1
		for (var i = 1; i < items.Length; i++)
		{
			var previous = Convert.ToInt64(items[i - 1]);
			var current = Convert.ToInt64(items[i]);

			if (current - previous != 1)
			{
				return false;
			}
		}

		return true;
	}

	public static IEnumerable<T> DistintBy<T, TResult>(this IEnumerable<T> source, Func<T, TResult> selector)
	{
		var seen = new HashSet<TResult>();

		foreach (var item in source)
		{
			if (seen.Add(selector(item)))
			{
				yield return item;
			}
		}
	}

	public static IEnumerable<KeyValuePair<TKey, int>> CountBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> keySelector)
	{
		var counts = new Dictionary<TKey, int>(20);

		foreach (var item in items)
		{
			var key = keySelector(item);

			if (counts.TryGetValue(key, out var currentCount))
			{
				counts[key] = currentCount + 1;
			}
			else
			{
				counts.Add(key, 1);
			}
		}

		return counts;
	}

	public static IEnumerable<KeyValuePair<T, int>> CountBy<T>(this IEnumerable<T> items)
	{
		var counts = new Dictionary<T, int>(20);

		foreach (var item in items)
		{
			if (counts.TryGetValue(item, out var currentCount))
			{
				counts[item] = currentCount + 1;
			}
			else
			{
				counts.Add(item, 1);
			}
		}

		return counts;
	}
}