namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((value, percentage) => value * percentage / 100);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((value, percentage) => value * percentage * 0.01),
		CreateFolded(100.0, 25.0),
		CreateFolded(50.0, 0.0),
		CreateFolded(50.0, 15.0)
	];
}