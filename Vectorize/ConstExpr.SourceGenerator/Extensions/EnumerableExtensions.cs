using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

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
	
	public static object? Sum(this IEnumerable<object?> source)
	{
		using var enumerator = source.GetEnumerator();
		
		if (!enumerator.MoveNext())
		{
			return null;
		}
		
		var sum = enumerator.Current;
		
		while (enumerator.MoveNext())
		{
			sum = Add(sum, enumerator.Current);
		}
		
		return sum;
	}
	
	private static object Add(object? a, object? b)
	{
		return a switch
		{
			sbyte aSByte when b is sbyte bSByte => aSByte + bSByte,
			byte aByte when b is byte bByte => aByte + bByte,
			short aShort when b is short bShort => aShort + bShort,
			ushort aUShort when b is ushort bUShort => aUShort + bUShort,
			int ai when b is int bi => ai + bi,
			float af when b is float bf => af + bf,
			double ad when b is double bd => ad + bd,
			decimal am when b is decimal bm => am + bm,
			_ => throw new NotSupportedException($"Cannot add {a?.GetType()} and {b?.GetType()}")
		};
	}
	
	public static object? Average(this IEnumerable<object?> source, ITypeSymbol elementType)
	{
		return elementType.SpecialType switch
		{
			SpecialType.System_Int32 => source.Cast<int>().Average(),
			SpecialType.System_Single => source.Cast<float>().Average(),
			SpecialType.System_Double => source.Cast<double>().Average(),
			SpecialType.System_Decimal => source.Cast<decimal>().Average(),
			_ => null,
		};
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

	public static bool IsNumericSequence(this IList<object?> items)
	{
		if (items.Count <= 1)
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
		for (var i = 1; i < items.Count; i++)
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
	
	public static bool IsZero(this IEnumerable<object?> items)
	{
		return items.All(a => a is 0 or 0L or (byte) 0 or (short) 0 or (sbyte) 0 or (ushort) 0 or (uint) 0 or (ulong) 0);
	}

	public static bool IsOne(this IEnumerable<object?> items)
	{
		return items.All(a => a is 1 or 1L or (byte) 1 or (short) 1 or (sbyte) 1 or (ushort) 1 or (uint) 1 or (ulong) 1);
	}
}