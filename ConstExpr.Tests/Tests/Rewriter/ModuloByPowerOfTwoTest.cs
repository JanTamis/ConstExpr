namespace ConstExpr.Tests.Rewriter;

/// <summary>x % power-of-two → x &amp; (pow - 1) for unsigned types.</summary>
[InheritsTests]
public class ModuloByPowerOfTwoTest : BaseTest<Func<uint, uint>>
{
	public override string TestMethod => GetString(x => x % 8u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x & 7u),
		Create(_ => 3u, [ 11u ]),
		Create(_ => 0u, [ 8u ]),
	];
}