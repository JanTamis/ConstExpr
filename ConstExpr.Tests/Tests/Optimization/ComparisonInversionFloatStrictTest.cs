namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Under Strict math a negated float equality still inverts (NaN-safe),
///   but a negated relational comparison must NOT invert (NaN flips the outcome).
/// </summary>
[InheritsTests]
public class ComparisonInversionFloatStrictTest : BaseTest<Func<float, float, (bool, bool)>>
{
	public override string TestMethod => GetString((a, b) => (!(a == b), !(a < b)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => (a != b, !(a < b))),
		Create((_, _) => (false, true), [ 1f, 1f ]),
		Create((_, _) => (true, false), [ 1f, 2f ]),
		Create((_, _) => (true, true), [ Single.NaN, 1f ])
	];
}