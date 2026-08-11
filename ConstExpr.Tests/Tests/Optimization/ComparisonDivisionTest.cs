namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v / c OP k isolates by multiplying the threshold across the comparison: v / c OP k => v OP k * c.
///   Uses c = 4 so the isolated threshold (4) is an exact integer despite the float division.
/// </summary>
[InheritsTests]
public class ComparisonDivisionTest : BaseTestWithRandomValues<Func<float, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x / 4 < 1, x / 4 > 1, x / 4 <= 1, x / 4 >= 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 4f, x > 4f, x <= 4f, x >= 4f)),
	];
}