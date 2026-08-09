namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathILogBTest : BaseTest<Func<double, int>>
{
	public override string TestMethod => GetString(x => System.Math.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
		CreateFolded(8.0),
		CreateFolded(Double.Epsilon), // smallest subnormal
		CreateFolded(0.0),
		CreateFolded(Double.PositiveInfinity),
		CreateFolded(Double.NaN)
	];
}