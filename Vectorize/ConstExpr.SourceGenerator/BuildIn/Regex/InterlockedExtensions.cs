// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Polyfill for Interlocked.Or which is not available in netstandard2.0.</summary>
internal static class InterlockedExtensions
{
	/// <summary>Atomically ORs the value of <paramref name="location"/> with <paramref name="value"/> and stores the result.</summary>
	public static uint Or(ref uint location, uint value)
	{
		var current = Volatile.Read(ref location);

		while (true)
		{
			var newValue = current | value;
			ref var loc = ref Unsafe.As<uint, int>(ref location);
			var actual = Interlocked.CompareExchange(ref loc, unchecked((int) newValue), unchecked((int) current));
			if (actual == unchecked((int) current))
				return current;
			current = unchecked((uint) actual);
		}
	}
}