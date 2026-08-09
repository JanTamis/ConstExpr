using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned division, non-"add" branch (magic fits in 32 bits): x / 3u.</summary>
[InheritsTests]
public class DivideGranlundMontgomeryUnsignedNonAddTest() : BaseTest<Func<uint, uint>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 3u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (uint) (x * 2863311531UL >> 32) >> 1),
		CreateFolded(10u),
		CreateFolded(3u),
		CreateFolded(0u),
		CreateFolded(2u)
	];
}