namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   (x % 2^n) != 0 DOES fold to (x &amp; (2^n - 1)) != 0 for a signed type with no non-negativity proof — see
///   <see cref="ModuloByPowerOfTwoZeroEqualityFoldsForSignedTest" /> for the == 0 counterpart.
/// </summary>
[InheritsTests]
public class ModuloByPowerOfTwoZeroEqualityFoldsForSignedNotEqualsTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x % 4 != 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 3) != 0),
		Create(_ => false, [ -4 ]),
		Create(_ => true, [ -5 ])
	];
}