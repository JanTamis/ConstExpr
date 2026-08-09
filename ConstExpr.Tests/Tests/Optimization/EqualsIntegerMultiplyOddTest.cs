namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v * c == k isolates to v == k / c when c is odd and k is exactly divisible by c — odd c makes
///   multiplication mod 2^n a bijection, so the exact quotient is the unique solution. Uses c = 11
///   (not 3): small odd constants like 3, 5, 7, 9 get strength-reduced to shift-add (e.g. x * 3 =>
///   (x &lt;&lt; 1) + x) upstream, before this strategy ever sees a MultiplyExpression — 11 survives
///   as a plain multiply.
/// </summary>
[InheritsTests]
public class EqualsIntegerMultiplyOddTest : BaseTest<Func<int, (bool, bool)>>
{
	public override string TestMethod => GetString(x => (x * 11 == 33, x * 11 != 33));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x == 3, x != 3)),
		CreateFolded(3),
		CreateFolded(4)
	];
}