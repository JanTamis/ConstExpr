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
		CreateFolded(0f),
		CreateFolded(10f),
		CreateFolded(4f)
	];
}