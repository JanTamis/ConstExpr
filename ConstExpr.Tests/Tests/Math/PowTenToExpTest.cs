namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowTenToExpTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(n => System.Math.Pow(10.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => Double.Exp10(n)),
		CreateFolded(3.0),
		CreateFolded(0.0)
	];
}