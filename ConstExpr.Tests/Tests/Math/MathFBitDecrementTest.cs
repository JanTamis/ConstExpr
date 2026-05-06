using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFBitDecrementTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.BitDecrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitDecrement(x);"),
		Create("return 1.9999999F;", 2f),
	];
}