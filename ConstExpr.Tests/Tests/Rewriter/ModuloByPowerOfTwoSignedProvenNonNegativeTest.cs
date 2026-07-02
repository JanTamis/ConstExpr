namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % power-of-two DOES fold to x &amp; (pow - 1) for a signed type once a sibling comparison in the
///   same condition proves x is non-negative.
/// </summary>
[InheritsTests]
public class ModuloByPowerOfTwoSignedProvenNonNegativeTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		if (x >= 0)
		{
			return x % 4;
		}

		return -1;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >= 0 ? x & 3 : -1)
	];
}