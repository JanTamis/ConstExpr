namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathDegreesToRadiansTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => Double.DegreesToRadians(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x * 0.017453292519943295),
	];
}