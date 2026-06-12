using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed division: x / d → multiply-shift without division.</summary>
[InheritsTests]
public class DivideGranlundMontgomerySignedTest() : BaseTest<Func<int, int>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => x / 6);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (int)((long)x * 715827883 >> 32) + ((int)((long)x * 715827883 >> 32) >>> 31);"),
		Create(_ => 1, [ 10 ]),
		Create(_ => 1, [ 6 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => -1, [ -7 ]),
		Create(_ => -2, [ -12 ])
	];
}