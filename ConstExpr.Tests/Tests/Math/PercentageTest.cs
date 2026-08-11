namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((value, percentage) => value * percentage / 100);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((value, percentage) => value * percentage * 0.01),
	];
}