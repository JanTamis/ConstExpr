namespace ConstExpr.Tests.Optimization;

/// <summary>
///   v * c == k is NOT isolated when k isn't evenly divisible by c, even for odd c: a solution still
///   exists somewhere via the modular inverse of c, but this strategy doesn't compute it, so it
///   declines rather than guessing (must NOT fold to "impossible" — a solution really does exist,
///   confirmed by the last concrete case). Uses c = 13 (not 3): small odd constants like 3 get
///   strength-reduced to shift-add upstream, before this strategy would ever see a MultiplyExpression
///   to decline on — 13 survives as a plain multiply.
/// </summary>
[InheritsTests]
public class EqualsIntegerMultiplyNotDivisibleDeclineTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x * 13 == 7);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x * 13 == 7),
	];
}