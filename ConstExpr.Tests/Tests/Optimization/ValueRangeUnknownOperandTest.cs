namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Vacuity check for value-range propagation. Identical in shape to
///   <see cref="ValueRangeBitmaskFoldTest" /> but with <c>|</c> instead of <c>&amp;</c>: an or with an
///   unknown operand is unbounded above, so nothing is settled and the guard must survive. This is
///   what separates "the pass fired" from "the pass folds whatever it is shown".
/// </summary>
[InheritsTests]
public class ValueRangeUnknownOperandTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var lane = n | 15;

		if (lane > 20)
		{
			return -1;
		}

		return lane;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Untouched by this pass. The guard is a ternary only because the partial rewriter puts it in
		// that form for every `if` — that happens with the flag off too.
		Create(n =>
		{
			var lane = n | 15;

			return lane > 20 ? -1 : lane;
		}),
	];
}