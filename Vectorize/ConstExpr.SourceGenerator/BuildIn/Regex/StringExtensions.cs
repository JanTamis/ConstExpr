// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Delegate used by StringExtensions.Create for filling a Span.</summary>
internal delegate void SpanFillAction<in TArg>(Span<char> span, TArg arg);

/// <summary>Polyfill for string.Create&lt;TState&gt; which is not available in netstandard2.0.</summary>
internal static class StringExtensions
{
	/// <summary>Creates a new string by providing the length and a callback that fills the characters.</summary>
	public static string Create<TState>(int length, TState state, SpanFillAction<TState> action)
	{
		if (length == 0)
			return String.Empty;

		var array = ArrayPool<char>.Shared.Rent(length);

		try
		{
			action(array.AsSpan(0, length), state);
			return new string(array, 0, length);
		}
		finally
		{
			ArrayPool<char>.Shared.Return(array);
		}
	}

	/// <summary>
	/// Polyfill for <c>MemoryExtensions.CommonPrefixLength</c> (available from .NET 8).
	/// Returns the length of the common prefix shared by <paramref name="span"/> and <paramref name="other"/>.
	/// </summary>
	public static int CommonPrefixLength(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
	{
		var length = Math.Min(span.Length, other.Length);

		for (var i = 0; i < length; i++)
		{
			if (span[i] != other[i])
				return i;
		}

		return length;
	}
}