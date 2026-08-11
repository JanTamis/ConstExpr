namespace ConstExpr.Tests.Math;

/// <summary>Math.Log2(double) -> FastLog2(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathLog2Test : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Log2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog2(x);"),
		CreateFolded(1.0),
		CreateFolded(8.0)
	];
}