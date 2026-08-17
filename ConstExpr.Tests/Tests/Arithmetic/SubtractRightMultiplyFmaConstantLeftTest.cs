namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SubtractRightMultiplyFmaConstantLeftTest : BaseTestWithRandomValues<Func<double, double, double>>
{
	public override string TestMethod => GetString((x, c) => x - 3 * c);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.MultiplyAddEstimate(c, -3D, x);", Unknown, Unknown)
	];
}