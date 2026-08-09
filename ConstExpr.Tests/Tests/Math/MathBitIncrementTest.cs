namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathBitIncrementTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.BitIncrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitIncrement(x);"),
		CreateFolded(1.9999999999999998)
	];
}