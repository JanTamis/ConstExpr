namespace ConstExpr.Tests.Math;

/// <summary>Math.Atanh(double) -> FastAtanh(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathAtanhTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Atanh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtanh(x);"),
	];
}