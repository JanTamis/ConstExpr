namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v &gt;&gt; c isolates by scaling the threshold by 2^c: the &lt;/&gt;= family keeps k as-is
///   (v &gt;&gt; c &lt; k => v &lt; k * 2^c), while the &lt;=/&gt; family bumps k by one first
///   (v &gt;&gt; c &lt;= k => v &lt; (k + 1) * 2^c), since &gt;&gt; floors (rounds toward negative
///   infinity) rather than truncating toward zero. k must be a bare literal (this strategy family
///   only matches a plain literal right operand, not one wrapped in a unary minus), so k = 0 here and
///   the negative side of the boundary is exercised through negative concrete x values instead —
///   including x = 0 sitting exactly on the bucket edge.
/// </summary>
[InheritsTests]
public class ComparisonRightShiftIsolationTest : BaseTestWithRandomValues<Func<int, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x >> 2 < 0, x >> 2 > 0, x >> 2 <= 0, x >> 2 >= 0));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 0, x >= 4, x < 4, x >= 0)),
	];
}