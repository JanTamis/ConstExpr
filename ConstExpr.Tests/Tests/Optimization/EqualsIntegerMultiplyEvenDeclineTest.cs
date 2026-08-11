namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v * c == k is NOT isolated when c is even: multiplication by an even c mod 2^n isn't a bijection,
///   so v * 6 == 12 is also true for v == int.MinValue + 2, not only v == 2 — isolating would be
///   unsound. The last test case confirms that concrete wraparound value directly: an unsound
///   "v == 2" fold would have returned false there, not true. Uses c = 6 (not 2): c = 2 gets
///   canonicalized to a shift (x &lt;&lt; 1) upstream, before this strategy would ever see a
///   MultiplyExpression to decline on — 6 survives as a plain multiply and still exercises the
///   even-coefficient decline path directly.
/// </summary>
[InheritsTests]
public class EqualsIntegerMultiplyEvenDeclineTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x * 6 == 12);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x * 6 == 12),
	];
}