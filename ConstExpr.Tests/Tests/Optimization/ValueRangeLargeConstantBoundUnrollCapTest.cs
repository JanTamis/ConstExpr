using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   A `for` loop whose bound is a compile-time constant larger than
///   <c>ConstExprAttribute.MaxUnrollIterations</c> (default 32) hits the unroll cap partway through and
///   falls back to a plain (non-unrolled) loop. The loop counter and body-local <c>bonus</c> were
///   already registered once by the partial-unroll attempt; re-visiting the original declarations
///   without resetting that state used to throw an <see cref="InvalidCastException" /> deep inside
///   <c>base.VisitForStatement</c> (swallowed by the rewriter's top-level exception handler), which
///   discarded all folding for the loop — <c>bonus</c> stayed as the unfolded <c>3 + 4</c> instead of
///   being resolved to <c>7</c>.
/// </summary>
[InheritsTests]
public class ValueRangeLargeConstantBoundUnrollCapTest() : BaseTest<Func<int, int>>(optimizations: OptimizationFlags.None)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < 100; i++)
		{
			var bonus = 3 + 4;
			sum += i + n + bonus;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var sum = 0;

			for (var i = 0; i < 100; i++)
			{
				sum += i + n + 7;
			}

			return sum;
		}, [ Unknown ])
	];
}