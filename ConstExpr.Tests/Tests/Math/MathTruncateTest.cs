namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathTruncateTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Truncate(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Truncate(x)),
		CreateFolded(3.7),
		CreateFolded(-3.2)
	];
}