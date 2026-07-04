using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Loop Unswitching must NOT fire when the <c>if</c> condition is not loop-invariant — here it
///   reads <c>result</c>, which is reassigned in both branches, so the condition's value can change
///   between iterations. The loop must be left unchanged (no hoisting, no duplication).
/// </summary>
[InheritsTests]
public class LoopUnswitchingNonInvariantTests() : BaseTest<Func<int, int, int>>(optimizations: OptimizationFlags.LoopUnswitching)
{
	public override string TestMethod => GetString((n, seed) =>
	{
		var result = seed;

		for (var i = 0; i < n; i++)
		{
			if (result < 100)
			{
				result += i;
			}
			else
			{
				result -= i;
			}
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// null expected body => assert the optimized body is identical to the original: the
		// non-invariant condition must not be unswitched.
		CreateDefault()
	];
}