using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace ConstExpr.SourceGenerator.Sample.Tests;

public class ReplaceTest
{
	public static ReadOnlySpan<int> ICustomCollection_32690199_Data => [ 0, 1, 4, 5, 5, 5, 6, 7, 8, 8, 8, 10, 11, 13, 13, 15, 16, 18, 18, 19 ];
	
	[Benchmark]
	[ArgumentsSource(nameof(Parameters))]
	public Dictionary<int, int> CountBy(Func<int, int> keySelector)
	{
		var counts = new Dictionary<int, int>();

		foreach (var item in ICustomCollection_32690199_Data)
		{
			var key = keySelector(item);

			ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, key, out _);
			count++;
		}

		return counts;
	}

	[Benchmark(Baseline = true)]
	[ArgumentsSource(nameof(Parameters))]
	public Dictionary<int, int> CountBy2(Func<int, int> keySelector)
	{
		var counts = new Dictionary<int, int>(19);

		foreach (var item in ICustomCollection_32690199_Data)
		{
			var key = keySelector(item);

			if (counts.TryGetValue(key, out var currentCount))
			{
				counts[key] = currentCount + 1;
			}
			else
			{
				counts.Add(key, 1);
			}
		}

		return counts;
	}

	public IEnumerable<object> Parameters() // for single argument it's an IEnumerable of objects (object)
	{
		yield return (Func<int, int>) (x => x);
	}
}