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
		Create((_, _) => false, [ 1, 2 ]),
		Create((_, _) => true, [ 2, 1 ]),
		Create((_, _) => true, [ 1, 1 ])
	];
}