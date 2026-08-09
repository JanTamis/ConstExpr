namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFBitIncrementTest : BaseTest<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.BitIncrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitIncrement(x);"),
		CreateFolded(1.9999999f)
	];
}