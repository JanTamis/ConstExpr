namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Value-range propagation over a counted loop. The bound is unknown, but the header still pins
///   <c>i</c> to <c>[0, …)</c> — it starts at zero and only ever goes up — so the guard against zero
///   is always true and disappears along with its nesting.
/// </summary>
[InheritsTests]
public class ValueRangeLoopCounterFoldTest : BaseTestWithRandomValues<Func<int, int>>
{

	protected override int MaxRandomMagnitudeBits => 5;
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < n; i++)
		{
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse — being always true is the point.
			if (i >= 0)
			{
				sum += i;
			}
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var sum = 0;

			for (var i = 0; i < n; i++)
			{
				sum += i;
			}

			return sum;
		}, [ Unknown ]),

		// Known bound unrolls away entirely: 0 + 1 + 2 + 3 + 4. Anchors the semantics of the shape
		// independently of the rewriter.
		CreateFolded(5)
	];
}