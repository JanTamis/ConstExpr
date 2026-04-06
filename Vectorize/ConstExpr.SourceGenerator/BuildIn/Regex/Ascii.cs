// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>
/// Polyfill for <c>System.Text.Ascii</c> (available from .NET 7+).
/// Provides ASCII-validity checks compatible with netstandard2.0.
/// </summary>
internal static class Ascii
{
	/// <summary>Returns true if every character in <paramref name="value"/> is a 7-bit ASCII character.</summary>
	public static bool IsValid(string? value)
	{
		return value != null && IsValid(value.AsSpan());
	}

	/// <summary>Returns true if every character in <paramref name="value"/> is a 7-bit ASCII character.</summary>
	public static bool IsValid(ReadOnlySpan<char> value)
	{
		foreach (var c in value)
		{
			if (c > 127)
				return false;
		}

		return true;
	}
}