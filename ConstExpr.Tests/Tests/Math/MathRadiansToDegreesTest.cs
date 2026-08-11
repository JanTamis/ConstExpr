namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathRadiansToDegreesTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => Double.RadiansToDegrees(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x * 57.29577951308232),
	];
}