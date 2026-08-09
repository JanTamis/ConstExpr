namespace ConstExpr.Tests.Math;

/// <summary>Math.Atan(double) → FastAtan(x) in FastMath mode, with algebraic constant folding.</summary>
[InheritsTests]
public class MathAtanTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Atan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan(x);"),
		CreateFolded(0.0)
	];
}