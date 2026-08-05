namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v - c OP k isolates to v OP k + c, with no operator flip.
/// </summary>
[InheritsTests]
public class ComparisonSubtractionDivisionTest : BaseTest<Func<float, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x - 3 < 1, x - 3 > 1, x - 3 <= 1, x - 3 >= 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 4f, x > 4f, x <= 4f, x >= 4f)),
		Create(_ => (true, false, true, false), [ 0f ]),
		Create(_ => (false, true, false, true), [ 10f ]),
		Create(_ => (false, false, true, true), [ 4f ])
	];
}