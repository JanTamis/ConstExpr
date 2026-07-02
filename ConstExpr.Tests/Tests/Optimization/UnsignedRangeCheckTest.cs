namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Verifies that an integer range check collapses into a single unsigned comparison:
///   x &gt;= 2 &amp;&amp; x &lt;= 10 => (uint)(x - 2) &lt;= 8U.
/// </summary>
[InheritsTests]
public class UnsignedRangeCheckTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x >= 2 && x <= 10);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (uint) (x - 2) <= 8U),
		Create(_ => true, [ 2 ]),
		Create(_ => true, [ 10 ]),
		Create(_ => false, [ 1 ]),
		Create(_ => false, [ 11 ]),
		Create(_ => false, [ -5 ])
	];
}