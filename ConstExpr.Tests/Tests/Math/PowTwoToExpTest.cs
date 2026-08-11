namespace ConstExpr.Tests.Math;

/// <summary>Tests for Pow algebraic strategies: literal base transformations.</summary>
[InheritsTests]
public class PowTwoToExpTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(n => System.Math.Pow(2.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => Double.Exp2(n)),
		CreateFolded(3.0),
		CreateFolded(0.0)
	];
}