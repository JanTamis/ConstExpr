namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SubtractRightMultiplyFmaConstantRightTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((x, c) => x - c * 3);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.MultiplyAddEstimate(c, -3D, x);", Unknown, Unknown)
	];
}