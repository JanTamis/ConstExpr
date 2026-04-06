// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Provides helper methods for converting hexadecimal characters.</summary>
internal static class HexConverter
{
	/// <summary>
	/// Converts a hex character to its integer value, or returns 0xFF if the character is not a valid hex digit.
	/// </summary>
	public static int FromChar(char c)
	{
		if (c >= '0' && c <= '9') return c - '0';
		if (c >= 'a' && c <= 'f') return c - 'a' + 10;
		if (c >= 'A' && c <= 'F') return c - 'A' + 10;
		return 0xFF;
	}
}