namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % 2 != 1, once a sibling comparison in the same condition proves x is non-negative, is eligible
///   to fold to T.IsEvenInteger(x) for a signed type. The modulo is first folded to (x &amp; 1) != 1 by
///   ModuloByPowerOfTwoStrategy (which proves the same non-negativity); the further simplification to
///   IsEvenInteger(x) is left to NotEqualsBitwiseAndEvenStrategy, which does not yet fire on a
///   parenthesized left operand (tracked separately — see EqualsBitwiseAndEvenStrategy for the
///   paren-unwrapping it's missing). This test pins the currently-correct intermediate form so a
///   regression doesn't silently reintroduce the unsafe direct-to-IsEvenInteger fold.
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
		Create(x => x >= 0 ? (x & 1) != 1 : false)
	];
}