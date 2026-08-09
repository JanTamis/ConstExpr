namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathBitDecrementTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.BitDecrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitDecrement(x);"),
		CreateFolded(2.0)
	];
}