namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFloorTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Floor(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Floor(x)),
		CreateFolded(3.7),
		CreateFolded(-3.2)
	];
}