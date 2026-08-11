namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCopySignTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((x, y) => System.Math.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCopySign<double, long>(x, y);"),
	];
}