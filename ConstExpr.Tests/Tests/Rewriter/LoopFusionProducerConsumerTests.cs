using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Producer-consumer fusion: the first loop writes <c>values[i]</c>, the second reads it. Both
///   sides touch the array exclusively as <c>values[i]</c> with the shared monotonic counter
///   (dependence distance 0), so iteration k of the consumer only depends on iteration k of the
///   producer — exactly what fusion preserves.
/// </summary>
[InheritsTests]
public class LoopFusionProducerConsumerTests() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	public override string TestMethod => GetString(n =>
	{
		var values = new int[n];
		var total = 0;

		for (var i = 0; i < n; i++)
		{
			values[i] = i + 1;
		}

		for (var i = 0; i < n; i++)
		{
			total += values[i];
		}

		return total;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var values = new int[n];
			var total = 0;

			for (var i = 0; i < n; i++)
			{
				values[i] = i + 1;
				total += values[i];
			}

			return total;
		}, [ Unknown ])
	];
}