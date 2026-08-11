namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFBitDecrementTest : BaseTestWithRandomValues<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.BitDecrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitDecrement(x);"),
	];
}