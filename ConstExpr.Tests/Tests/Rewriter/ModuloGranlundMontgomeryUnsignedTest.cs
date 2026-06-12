using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned modulo: x % d → multiply-shift without division.</summary>
[InheritsTests]
public class ModuloGranlundMontgomeryUnsignedTest() : BaseTest<Func<uint, uint>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => x % 7u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x - ((uint)((ulong)x * 613566757UL >> 32) + (x - (uint)((ulong)x * 613566757UL >> 32) >> 1) >> 2) * 7U;"),
		Create(_ => 3u, [ 10u ]),
		Create(_ => 0u, [ 7u ]),
		Create(_ => 0u, [ 0u ]),
		Create(_ => 6u, [ 6u ])
	];
}