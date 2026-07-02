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
		Create((_, _) => (false, true), [ 1f, 1f ]),
		Create((_, _) => (true, false), [ 1f, 2f ]),
		Create((_, _) => (true, true), [ 3f, 2f ])
	];
}