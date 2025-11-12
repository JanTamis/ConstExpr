using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Comparers;

/// <summary>
/// Comparer for ordering using directives: System* first, then alphabetical (ordinal)
/// </summary>
internal sealed class UsingComparer : IComparer<string>
{
	public static readonly UsingComparer Instance = new();
	public int Compare(string? x, string? y)
	{
		if (ReferenceEquals(x, y)) return 0;
		if (x is null) return -1;
		if (y is null) return 1;
		var xs = x.StartsWith("System", StringComparison.Ordinal);
		var ys = y.StartsWith("System", StringComparison.Ordinal);
		if (xs != ys)
		{
			// System* should come first
			return xs ? -1 : 1;
		}
		return string.Compare(x, y, StringComparison.Ordinal);
	}
}

#pragma warning restore RSEXPERIMENTAL002