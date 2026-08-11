namespace ConstExpr.Tests.Math;

/// <summary>Math.Cbrt(double) → FastCbrt(x) in FastMath mode.</summary>
[InheritsTests]
public class MathCbrtTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Cbrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCbrt(x);"),
	];
}