namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % 2 != 1, once a sibling comparison in the same condition proves x is non-negative, folds to
///   T.IsEvenInteger(x) for a signed type. The modulo is first folded to (x &amp; 1) != 1 by
///   ModuloByPowerOfTwoStrategy (which proves the same non-negativity), then NotEqualsBitwiseAndEvenStrategy
///   folds that to IsEvenInteger(x).
/// </summary>
[InheritsTests]
public class NotEqualsModuloEvenSignedProvenNonNegativeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(x =>
	{
		if (x >= 0)
		{
			return x % 2 != 1;
		}

		return false;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >= 0 ? Int32.IsEvenInteger(x) : false)
	];
}