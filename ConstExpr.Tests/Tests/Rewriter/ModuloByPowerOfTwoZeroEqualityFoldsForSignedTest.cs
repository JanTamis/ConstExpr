namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   (x % 2^n) == 0 DOES fold to (x &amp; (2^n - 1)) == 0 for a signed type with no non-negativity proof —
///   divisibility by a power of two is sign-invariant (-4 % 4 == 0 and -4 &amp; 3 == 0; -5 % 4 == -1 (nonzero)
///   and -5 &amp; 3 == 3 (nonzero)), unlike the bare x % 4 fold guarded in
///   <see cref="ModuloByPowerOfTwoSignedNotRewrittenTest" />.
/// </summary>
[InheritsTests]
public class ModuloByPowerOfTwoZeroEqualityFoldsForSignedTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x => x % 4 == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x & 3) == 0),
		Create(_ => true, [ -4 ]),
		Create(_ => false, [ -5 ])
	];
}