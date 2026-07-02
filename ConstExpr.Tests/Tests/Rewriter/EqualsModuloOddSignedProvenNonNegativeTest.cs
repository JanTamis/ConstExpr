namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % 2 == 1 DOES fold to T.IsOddInteger(x) for a signed type once a sibling comparison in the
///   same condition proves x is non-negative.
/// </summary>
[InheritsTests]
public class EqualsModuloOddSignedProvenNonNegativeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x =>
	{
		if (x >= 0)
		{
			return x % 2 == 1;
		}

		return false;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >= 0 ? Int32.IsOddInteger(x) : false)
	];
}