namespace ConstExpr.Tests.Math;

/// <summary>Math.Log10(double) → FastLog10(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathLog10Test : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Log10(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog10(x);"),
	];
}