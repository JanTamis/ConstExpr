using System;
using System.Text;

namespace ConstExpr.SourceGenerator;
#pragma warning disable RSEXPERIMENTAL002

/// <summary>
/// Simple thread-static StringBuilder pool to reduce allocations when composing large strings.
/// </summary>
internal static class StringBuilderCache
{
	[ThreadStatic]
	private static StringBuilder? _cachedInstance;

	public static StringBuilder Acquire(int capacity = 256)
	{
		var sb = _cachedInstance;

		if (sb is not null)
		{
			_cachedInstance = null;
			sb.Clear();

			if (sb.Capacity < capacity)
			{
				sb.EnsureCapacity(capacity);
			}

			return sb;
		}

		return new StringBuilder(capacity);
	}

	public static string GetStringAndRelease(StringBuilder sb)
	{
		var result = sb.ToString();

		// Limit retained capacity to avoid holding onto very large buffers
		if (sb.Capacity > 4096)
		{
			sb.Capacity = 4096;
		}

		_cachedInstance = sb;

		return result;
	}
}

#pragma warning restore RSEXPERIMENTAL002