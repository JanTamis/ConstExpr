using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathBitDecrementTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.BitDecrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitDecrement(x);"),
		Create("return 1.9999999999999998D;", 2.0),
	];
}