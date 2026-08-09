namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCeilingTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Ceiling(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Ceiling(x)),
		CreateFolded(3.2),
		CreateFolded(-3.7)
	];
}