namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathILogBTest : BaseTest<Func<double, int>>
{
	public override string TestMethod => GetString(x => System.Math.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
		Create(_ => 3, [ 8.0 ]),
		Create(_ => -1074, [ Double.Epsilon ]), // smallest subnormal
		Create(_ => Int32.MinValue, [ 0.0 ]),
		Create(_ => Int32.MaxValue, [ Double.PositiveInfinity ]),
		Create(_ => Int32.MaxValue, [ Double.NaN ])
	];
}