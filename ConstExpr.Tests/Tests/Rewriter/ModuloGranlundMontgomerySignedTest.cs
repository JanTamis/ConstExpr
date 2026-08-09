using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery signed modulo: x % d → multiply-shift without division.</summary>
[InheritsTests]
public class ModuloGranlundMontgomerySignedTest() : BaseTest<Func<int, int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x % 6);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x - ((int) (x * 715827883L >> 32) - (x >> 31)) * 6),
		CreateFolded(10),
		CreateFolded(6),
		CreateFolded(0),
		CreateFolded(-7)
	];
}