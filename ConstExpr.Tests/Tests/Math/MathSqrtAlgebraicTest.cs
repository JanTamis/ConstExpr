namespace ConstExpr.Tests.Math;

/// <summary>Sqrt(x * x) → Abs(x): algebraic identity for pure expressions.</summary>
[InheritsTests]
public class MathSqrtAlgebraicTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x * x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAbs<double, long>(x);")
	];
}