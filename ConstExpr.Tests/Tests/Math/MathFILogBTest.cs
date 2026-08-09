namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFILogBTest : BaseTest<Func<float, int>>
{
	public override string TestMethod => GetString(x => MathF.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
		CreateFolded(8f),
		CreateFolded(Single.Epsilon), // smallest subnormal
		CreateFolded(0f),
		CreateFolded(Single.PositiveInfinity),
		CreateFolded(Single.NaN)
	];
}