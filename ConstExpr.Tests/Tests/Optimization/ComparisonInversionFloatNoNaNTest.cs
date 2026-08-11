using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   With FastMathFlags.NoNaN both the negated equality and the negated relational comparison invert.
/// </summary>
[InheritsTests]
public class ComparisonInversionFloatNoNaNTest() : BaseTestWithRandomValues<Func<float, float, (bool, bool)>>(FastMathFlags.NoNaN)
{
	public override string TestMethod => GetString((a, b) => (!(a == b), !(a < b)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => (a != b, a >= b)),
	];
}