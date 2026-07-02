namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Verifies shift-based zero tests on unsigned integers collapse to one comparison:
///   (x &gt;&gt; 4) == 0 => x &lt; 16U and (x &gt;&gt; 4) != 0 => x &gt;= 16U.
/// </summary>
[InheritsTests]
public class RightShiftZeroCompareTest : BaseTest<Func<uint, (bool, bool)>>
{
	public override string TestMethod => GetString(x => (x >> 4 == 0, x >> 4 != 0));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 16U, x >= 16U)),
		Create(_ => (true, false), [ 0u ]),
		Create(_ => (true, false), [ 15u ]),
		Create(_ => (false, true), [ 16u ]),
		Create(_ => (false, true), [ UInt32.MaxValue ])
	];
}