namespace ConstExpr.Tests.Math;

/// <summary>Math.Exp2(double) → FastExp2(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathExp2Test : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => Double.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);"),
	];
}