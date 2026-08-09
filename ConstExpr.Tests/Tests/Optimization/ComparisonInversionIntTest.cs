namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Verifies that a negated integer comparison is inverted: !(a &lt; b) => a &gt;= b.
/// </summary>
[InheritsTests]
public class ComparisonInversionIntTest : BaseTest<Func<int, int, bool>>
{
	public override string TestMethod => GetString((a, b) => !(a < b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => a >= b),
		CreateFolded(1, 2),
		CreateFolded(2, 1),
		CreateFolded(1, 1)
	];
}