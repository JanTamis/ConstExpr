namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFILogBTest : BaseTestWithRandomValues<Func<float, int>>
{
	public override string TestMethod => GetString(x => MathF.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
	];
}