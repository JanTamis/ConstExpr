namespace ConstExpr.Tests.Math;

[InheritsTests]
public class HypotenuseTest : BaseTest<Func<int, int, double>>
{
	public override string TestMethod => GetString((a, b) => System.Math.Sqrt(a * a + b * b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.Sqrt(a * a + b * b)),
		CreateFolded(3, 4),
		CreateFolded(5, 12),
		CreateFolded(0, 10)
	];
}