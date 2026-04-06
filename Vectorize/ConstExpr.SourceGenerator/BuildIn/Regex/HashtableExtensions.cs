// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Extension methods for <see cref="Hashtable"/> to provide TryGetValue semantics.</summary>
internal static class HashtableExtensions
{
	/// <summary>
	/// Tries to get the value associated with the specified key from a <see cref="Hashtable"/>.
	/// </summary>
	public static bool TryGetValue(this Hashtable ht, int key, out int value)
	{
		var obj = ht[key];

		if (obj is int i)
		{
			value = i;
			return true;
		}
		value = 0;
		return false;
	}
}