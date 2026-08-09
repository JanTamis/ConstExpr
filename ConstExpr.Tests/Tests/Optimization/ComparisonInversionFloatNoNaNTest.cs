using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   With FastMathFlags.NoNaN both the negated equality and the negated relational comparison invert.
/// </summary>
[InheritsTests]
public class ComparisonInversionFloatNoNaNTest() : BaseTest<Func<float, float, (bool, bool)>>(FastMathFlags.NoNaN)
{
	public override string TestMethod => GetString((a, b) => (!(a == b), !(a < b)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => (a != b, a >= b)),
		CreateFolded(1f, 1f),
		CreateFolded(1f, 2f),
		CreateFolded(3f, 2f)
	];
}