namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFCopySignTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((x, y) => MathF.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCopySign<float, int>(x, y);"),
	];
}