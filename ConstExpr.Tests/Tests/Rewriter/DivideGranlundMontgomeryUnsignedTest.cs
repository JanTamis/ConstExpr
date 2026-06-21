using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned division: x / d → multiply-shift without division.</summary>
[InheritsTests]
public class DivideGranlundMontgomeryUnsignedTest() : BaseTest<Func<uint, uint>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 7u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (uint)(x * 613566757UL >> 32) + (x - (uint)(x * 613566757UL >> 32) >> 1) >> 2),
		Create(_ => 1u, [ 10u ]),
		Create(_ => 1u, [ 7u ]),
		Create(_ => 0u, [ 0u ]),
		Create(_ => 0u, [ 6u ]),
		Create(_ => 2u, [ 14u ])
	];
}