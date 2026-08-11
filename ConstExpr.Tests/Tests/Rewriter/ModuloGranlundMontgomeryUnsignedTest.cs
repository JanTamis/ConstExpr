using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

/// <summary>Granlund-Montgomery unsigned modulo: x % d → multiply-shift without division.</summary>
[InheritsTests]
public class ModuloGranlundMontgomeryUnsignedTest() : BaseTestWithRandomValues<Func<uint, uint>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	public override string TestMethod => GetString(x => x % 7u);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x =>
		{
			var castVal = (uint) (x * 613566757UL >> 32);

			return x - (castVal + (x - castVal >> 1) >> 2) * 7U;
		}),
	];
}