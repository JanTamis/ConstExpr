namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFAtan2Test : BaseTest<Func<float, float, float>>
{
	public override string TestMethod => GetString((y, x) => MathF.Atan2(y, x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan2(y, x);"),
		Create((_, _) => 0F, [ 0f, 2f ])
	];
}