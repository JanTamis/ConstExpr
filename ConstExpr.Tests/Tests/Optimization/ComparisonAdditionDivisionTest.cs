namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v + c OP k isolates to v OP k - c, with no operator flip (unlike the multiply case, adding a
///   constant never changes the comparison's direction).
/// </summary>
[InheritsTests]
public class ComparisonAdditionDivisionTest : BaseTestWithRandomValues<Func<float, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x + 3 < 1, x + 3 > 1, x + 3 <= 1, x + 3 >= 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < -2f, x > -2f, x <= -2f, x >= -2f)),
	];
}