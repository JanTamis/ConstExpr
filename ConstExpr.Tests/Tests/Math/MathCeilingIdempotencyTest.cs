namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCeilingIdempotencyTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(x => System.Math.Ceiling(System.Math.Ceiling(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Ceiling(x))
	];
}