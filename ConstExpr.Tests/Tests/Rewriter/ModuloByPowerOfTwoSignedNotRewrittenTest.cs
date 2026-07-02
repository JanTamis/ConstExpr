namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   x % power-of-two must NOT fold to x &amp; (pow - 1) for a signed type without a proof that x is
///   non-negative — -5 % 4 == -1 in C#, but -5 &amp; 3 == 3.
/// </summary>
[InheritsTests]
public class ModuloByPowerOfTwoSignedNotRewrittenTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x % 4);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}