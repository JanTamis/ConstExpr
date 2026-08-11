namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathILogBTest : BaseTestWithRandomValues<Func<double, int>>
{
	public override string TestMethod => GetString(x => System.Math.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
	];
}