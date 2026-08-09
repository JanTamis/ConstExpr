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
		Create((a, b) => (a != b, a >= b)),
		CreateFolded(1f, 1f),
		CreateFolded(1f, 2f),
		CreateFolded(Single.NaN, 1f)
	];
}