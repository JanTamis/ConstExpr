namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowNegHalfExpTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Pow(x, -0.5));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ReciprocalSqrtEstimate(x)),
	];
}