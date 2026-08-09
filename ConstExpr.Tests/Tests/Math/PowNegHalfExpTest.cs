namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowNegHalfExpTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Pow(x, -0.5));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ReciprocalSqrtEstimate(x)),
		CreateFolded(4.0),
		CreateFolded(1.0)
	];
}