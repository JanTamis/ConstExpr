using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned division: x / d → multiply-shift without division.</summary>
[InheritsTests]
public class DivideGranlundMontgomeryUnsignedTest() : BaseTest<Func<uint, uint>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x / 7u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var castVal = (uint) (x * 613566757UL >> 32);

			return castVal + (x - castVal >> 1) >> 2;
		}),
		CreateFolded(10u),
		CreateFolded(7u),
		CreateFolded(0u),
		CreateFolded(6u),
		CreateFolded(14u)
	];
}