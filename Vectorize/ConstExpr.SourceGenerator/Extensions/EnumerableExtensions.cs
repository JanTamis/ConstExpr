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
}