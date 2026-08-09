namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathSqrtTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Sqrt(x)),
		CreateFolded(9.0),
		CreateFolded(4.0)
	];
}