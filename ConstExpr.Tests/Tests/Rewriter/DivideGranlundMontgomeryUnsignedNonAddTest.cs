using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned division, non-"add" branch (magic fits in 32 bits): x / 3u.</summary>
[InheritsTests]
public class DivideGranlundMontgomeryUnsignedNonAddTest() : BaseTest<Func<uint, uint>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 3u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (uint)(x * 2863311531UL >> 32) >> 1),
		Create(_ => 3u, [ 10u ]),
		Create(_ => 1u, [ 3u ]),
		Create(_ => 0u, [ 0u ]),
		Create(_ => 0u, [ 2u ])
	];
}