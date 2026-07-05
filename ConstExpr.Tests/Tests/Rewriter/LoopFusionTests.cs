using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for Loop Fusion (LF).
///   Two directly adjacent loops with identical iteration spaces and independent bodies are merged
///   into one loop, so the counter and bound check run once per iteration instead of twice.
///   Note: LF runs after ConstExprPartialRewriter, so inputs are Unknown — the loop bound is
///   unknown (not unrolled) and both loops survive partial evaluation.
/// </summary>
[InheritsTests]
public class LoopFusionTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.LoopFusion)
{
	/// <summary>
	///   Two adjacent loops over the same range with disjoint accumulators — no cross-body
	///   dependence, so the bodies are simply concatenated.
	/// </summary>
	public override string TestMethod => GetString((n, seed) =>
	{
		var sum = 0;
		var prod = seed;

		for (var i = 0; i < n; i++)
		{
			sum += i;
		}

		for (var i = 0; i < n; i++)
		{
			prod += i;
		}

		return sum + prod;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((n, seed) =>
		{
			var sum = 0;
			var prod = seed;

			for (var i = 0; i < n; i++)
			{
				sum += i;
				prod += i;
			}

			return sum + prod;
		}, [ Unknown, Unknown ])
	];
}