namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Under Strict math a negated float equality still inverts (NaN-safe),
///   but a negated relational comparison must NOT invert (NaN flips the outcome).
/// </summary>
[InheritsTests]
public class ComparisonInversionFloatStrictTest : BaseTestWithRandomValues<Func<float, float, (bool, bool)>>
{
	public override string TestMethod => GetString((a, b) => (!(a == b), !(a < b)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => (a != b, a >= b)),
	];
}