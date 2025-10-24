using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConstExpr.SourceGenerator.Comparers;

public class ValueOrCollectionEqualityComparer<T> : IEqualityComparer<T>
{
	public static ValueOrCollectionEqualityComparer<T> Instance = new();

	public bool Equals(T? x, T? y)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		// Handle strings as values, not collections
		if (x is string || y is string)
		{
			return x.Equals(y);
		}

		// Handle Span/ReadOnlySpan via reflection (content equality without ByRef indexer)
		if (IsSpanLike(x) && IsSpanLike(y) && x.GetType() == y.GetType())
		{
			return SpanLikeEquals(x, y);
		}

		// Both must be enumerable and of compatible types
		if (x is IEnumerable xEnum && y is IEnumerable yEnum && x.GetType() == y.GetType())
		{
			return xEnum.Cast<T>().SequenceEqual(yEnum.Cast<T>(), this);
		}

		// Fall back to default equality
		return x.Equals(y);
	}

	public int GetHashCode(T? obj)
	{
		if (obj is null)
		{
			return 0;
		}

		// Handle strings as values
		if (obj is string str)
		{
			return str.GetHashCode();
		}

		// Handle Span/ReadOnlySpan via reflection (content hash)
		if (IsSpanLike(obj))
		{
			return GetSpanLikeHashCode(obj);
		}

		if (obj is IEnumerable enumerable)
		{
			unchecked
			{
				var hash = 19;

				foreach (var item in enumerable)
				{
					hash *= 31 + ValueOrCollectionEqualityComparer<object>.Instance.GetHashCode(item);
				}

				return hash;
			}
		}

		return obj.GetHashCode();
	}

	// Helpers
	private static bool IsSpanLike(T obj)
	{
		var type = obj.GetType();
		return type.IsGenericType
		       && type.Namespace == "System"
		       && (type.Name == "Span`1" || type.Name == "ReadOnlySpan`1");
	}

	private bool SpanLikeEquals(object x, object y)
	{
		// Prefer using ToArray() instance method to avoid ByRef indexer
		if (TryToArray(x, out var xa) && TryToArray(y, out var ya))
		{
			return xa.Cast<T>().SequenceEqual(ya.Cast<T?>(), this);
		}

		// Fallback: compare element type and Length only (best-effort, non-throwing)
		TryGetSpanLikeMembers(x, out var xLenProp, out _, out var xElemType);
		TryGetSpanLikeMembers(y, out var yLenProp, out _, out var yElemType);

		if (!Equals(xElemType, yElemType))
		{
			return false;
		}

		var xLen = xLenProp != null ? (int)xLenProp.GetValue(x) : -1;
		var yLen = yLenProp != null ? (int)yLenProp.GetValue(y) : -2;
		return xLen == yLen;
	}

	private int GetSpanLikeHashCode(T span)
	{
		// Prefer ToArray to get stable content hash without ByRef indexer
		if (TryToArray(span, out var array))
		{
			unchecked
			{
				var hash = 19;
				foreach (var item in array)
				{
					hash *= 31 + ValueOrCollectionEqualityComparer<object>.Instance.GetHashCode(item);
				}
				return hash;
			}
		}

		// Fallback: element type + length based hash
		TryGetSpanLikeMembers(span, out var lenProp, out _, out var elemType);
		var length = lenProp != null ? (int)lenProp.GetValue(span) : 0;
		unchecked
		{
			var h = 17;
			h = h * 31 + (elemType?.GetHashCode() ?? 0);
			h = h * 31 + length;
			return h;
		}
	}

	private static bool TryGetSpanLikeMembers(object obj, out PropertyInfo length, out PropertyInfo indexer, out System.Type elementType)
	{
		var type = obj.GetType();
		if (type.IsGenericType && type.Namespace == "System" && (type.Name == "Span`1" || type.Name == "ReadOnlySpan`1"))
		{
			length = type.GetProperty("Length");
			indexer = type.GetProperty("Item", [typeof(int)]);
			elementType = type.GetGenericArguments()[0];
			return length != null && elementType != null; // do not require indexer due to ByRef issues
		}

		length = null;
		indexer = null;
		elementType = null;
		return false;
	}

	private static bool TryToArray(object spanLike, out Array array)
	{
		var type = spanLike.GetType();
		// try instance ToArray()
		var toArray = type.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
		if (toArray != null)
		{
			var result = toArray.Invoke(spanLike, null) as Array;
			if (result != null)
			{
				array = result;
				return true;
			}
		}

		array = null;
		return false;
	}
}