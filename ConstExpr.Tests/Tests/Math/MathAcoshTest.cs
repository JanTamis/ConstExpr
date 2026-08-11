namespace ConstExpr.Tests.Math;

/// <summary>Math.Acosh(double) -> FastAcosh(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathAcoshTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Acosh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcosh(x);"),
	];
}