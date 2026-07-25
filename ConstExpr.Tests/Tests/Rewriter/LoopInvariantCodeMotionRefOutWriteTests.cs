using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Regression test: a variable only ever written via an <c>out</c>/<c>ref</c> argument inside the
///   loop (e.g. <c>int.TryParse(s, out value)</c>) must still count as "written in the loop". LICM's
///   <c>LoopInvariance.CollectWrittenInLoop</c> previously only recognised plain assignments and
///   ++/--, so a declaration reading such a variable looked invariant and was hoisted using the
///   stale pre-loop value instead of the per-iteration one.
/// </summary>
[InheritsTests]
public class LoopInvariantCodeMotionRefOutWriteTests() : BaseTest<Func<string, int, int, int>>(optimizations: OptimizationFlags.LoopInvariantCodeMotion)
{
	// `result = value` reads the incoming value before the loop overwrites it. `step` is read
	// twice per iteration so the partial rewriter does not inline it away.
	public override string TestMethod => GetString((s, n, value) =>
	{
		var result = value;

		for (var i = 0; i < n; i++)
		{
			Int32.TryParse(s, out value);
			var step = value * 2;
			result += step;
			result += step;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// `value` is (re)written every iteration via `out value`, so `step` is NOT invariant and
		// must not be hoisted out of the loop.
		Create((s, n, value) =>
		{
			var result = value;

			for (var i = 0; i < n; i++)
			{
				Int32.TryParse(s, out value);
				var step = value << 1;
				result += step;
				result += step;
			}

			return result;
		}, [ Unknown, Unknown, Unknown ])
	];
}