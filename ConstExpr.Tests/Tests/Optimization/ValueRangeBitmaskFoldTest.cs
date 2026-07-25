namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Value-range propagation over a bitmask. <c>n &amp; 15</c> cannot exceed 15 whatever <c>n</c>
///   holds, so the guard against 20 can never be taken and both it and its branch disappear.
///   This is the one operator the analysis handles with only a single side known, which is why the
///   unknown parameter does not stop it — contrast <see cref="ValueRangeUnknownOperandTest" />.
/// </summary>
[InheritsTests]
public class ValueRangeBitmaskFoldTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var lane = n & 15;

		if (lane > 20)
		{
			return -1;
		}

		return lane;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The guard and its branch are gone; the local survives because nothing after this pass inlines
		// a single use.
		Create(n =>
		{
			var lane = n & 15;

			return lane;
		}, [ Unknown ]),

		// A known input folds through the interpreter before the pass sees anything.
		Create(_ => 5, [ 21 ])
	];
}