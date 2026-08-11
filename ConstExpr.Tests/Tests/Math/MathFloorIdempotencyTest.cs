namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFloorIdempotencyTest : BaseTestWithRandomValues<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Floor(System.Math.Floor(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Floor(x))
	];
}