namespace ConstExpr.Tests.Optimization;

/// <summary>
///   The safety check that keeps value-range propagation honest, and the counterpart to
///   <see cref="ValueRangeLoopCounterFoldTest" />: the body assigns to the counter, so the <c>for</c>
///   header no longer describes what <c>i</c> holds by the time the guard runs. It genuinely does not
///   — a negative <c>n</c> drives <c>i</c> below zero — and the guard must survive untouched.
///   <para>
///     The same rule is what stops a <c>while</c> condition under an enclosing bound from being
///     folded into an infinite loop.
///   </para>
/// </summary>
[InheritsTests]
public class ValueRangeMutatedCounterTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			i += n;

			if (i >= 0)
			{
				sum += i;
			}
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unchanged: the guard stays exactly where it was.
		CreateDefault(),

		// n = 5 advances i to 5 in the body and 6 in the incrementor, so the second test of i < n
		// already fails. One pass, sum 5.
		Create(_ => 5, [ 5 ])
	];
}