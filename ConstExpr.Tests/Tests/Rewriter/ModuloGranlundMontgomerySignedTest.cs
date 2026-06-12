using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed modulo: x % d → multiply-shift without division.</summary>
[InheritsTests]
public class ModuloGranlundMontgomerySignedTest() : BaseTest<Func<int, int>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => x % 6);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x - ((int)((long)x * 715827883 >> 32) + ((int)((long)x * 715827883 >> 32) >>> 31)) * 6;"),
		Create(_ => 4, [ 10 ]),
		Create(_ => 0, [ 6 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => -1, [ -7 ])
	];
}