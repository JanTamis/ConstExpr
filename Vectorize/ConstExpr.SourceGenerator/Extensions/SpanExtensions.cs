using System;
using System.Text;

namespace ConstExpr.SourceGenerator.Extensions;

public static class SpanExtensions
{
	public static bool IsZero<T>(this ReadOnlySpan<T> span)
	{
		for (var i = 0; i < span.Length; i++)
		{
			if (!span[i].Equals(Convert.ChangeType(0, span[i].GetType())))
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsOne<T>(this ReadOnlySpan<T> span)
	{
		for (var i = 0; i < span.Length; i++)
		{
			if (span[i] is not 1 and not 1L and not 1U and not 1UL and not 1f or 1d and not 1 and not (short)1 and not (sbyte)1 and not (ushort)1 and not (byte) 1 and not (sbyte) 1)
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsSame<T>(this ReadOnlySpan<T> span, T item)
	{
		for (var i = 0; i < span.Length; i++)
		{
			if (span[i]?.Equals(item) is false)
			{
				return false;
			}
		}

		return true;
	}

	public static string Join<T, TResult>(this ReadOnlySpan<T> span, string separator, Func<T, TResult> selector)
	{
		if (span.Length == 0)
		{
			return string.Empty;
		}

		var result = new StringBuilder(span.Length * 2);
		result.Append(selector(span[0]));

		for (var i = 1; i < span.Length; i++)
		{
			result.Append(separator);
			result.Append(selector(span[i]));
		}

		return result.ToString();
	}

	public static string Join<T, TResult>(this ReadOnlySpan<T> span, string separator, int count, Func<T, TResult> selector)
	{
		if (span.Length == 0 || count == 0)
		{
			return String.Empty;
		}

		var result = new StringBuilder(span.Length * 2);

		while (true)
		{
			for (var i = 0; i < span.Length; i++)
			{
				if (count <= 0)
				{
					return result.ToString();
				}

				result.Append(selector(span[i]));

				if (count > 1)
				{
					result.Append(separator);
				}

				count--;
			}

			if (count <= 0)
			{
				return result.ToString();
			}
		}
	}

	public static string JoinWithPadding<T, TResult>(this ReadOnlySpan<T> span, string separator, int count, T item, Func<T, TResult> selector)
	{
		var result = new StringBuilder(span.Length * 2);

		if (span.Length == 0 || count == 0)
		{
			return String.Empty;
		}

		for (var i = 0; i < span.Length && count != 0; i++)
		{
			if (i > 0)
			{
				result.Append(separator);
			}

			result.Append(selector(span[i]));

			count--;
		}

		for (var i = 0; i < count; i++)
		{
			result.Append(separator);
			result.Append(selector(item));
		}

		return result.ToString();
	}

	public static bool IsSequence<T>(this ReadOnlySpan<T> span, Type type)
	{
		for (var i = 0; i < span.Length; i++)
		{
			if (!Convert.ChangeType(i, type).Equals(span[i]))
			{
				return false;
			}
		}

		return true;
	}
}