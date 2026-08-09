using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed division, "magic &lt; 0" + "shift &gt; 0" branch: x / 7.</summary>
[InheritsTests]
public class DivideGranlundMontgomerySignedNegativeMagicTest() : BaseTest<Func<int, int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 7);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => ((int) (-(1840700269L * x) >> 32) + x >> 2) - (x >> 31)),
		CreateFolded(10),
		CreateFolded(7),
		CreateFolded(0),
		CreateFolded(-7),
		CreateFolded(-100)
	];
}