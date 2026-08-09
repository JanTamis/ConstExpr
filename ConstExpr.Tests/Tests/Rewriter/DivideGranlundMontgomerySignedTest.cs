using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed division: x / d → multiply-shift without division.</summary>
[InheritsTests]
public class DivideGranlundMontgomerySignedTest() : BaseTest<Func<int, int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 6);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (int) (x * 715827883L >> 32) - (x >> 31)),
		CreateFolded(10),
		CreateFolded(6),
		CreateFolded(0),
		CreateFolded(-7),
		CreateFolded(-12)
	];
}