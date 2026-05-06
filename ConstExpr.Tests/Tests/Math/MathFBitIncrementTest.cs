using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFBitIncrementTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.BitIncrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitIncrement(x);"),
		Create("return 2F;", 1.9999999f),
	];
}