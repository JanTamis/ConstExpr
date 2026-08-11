namespace ConstExpr.Tests.Math;

/// <summary>Math.Tan(double) → FastTan(x) in FastMath mode, with algebraic constant folding.</summary>
[InheritsTests]
public class MathTanTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);"),
	];
}