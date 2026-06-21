using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed division, "magic &lt; 0" + "shift &gt; 0" branch: x / 7.</summary>
[InheritsTests]
public class DivideGranlundMontgomerySignedNegativeMagicTest() : BaseTest<Func<int, int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 7);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => ((int)(x * -1840700269L >> 32) + x >> 2) - (x >> 31)),
		Create(_ => 1, [ 10 ]),
		Create(_ => 1, [ 7 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => -1, [ -7 ]),
		Create(_ => -14, [ -100 ])
	];
}