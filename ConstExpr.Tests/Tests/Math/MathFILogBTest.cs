namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFILogBTest : BaseTest<Func<float, int>>
{
	public override string TestMethod => GetString(x => MathF.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastILogB(x);"), // Unknown args → emit fast helper
		Create(_ => 3, [ 8f ]),
		Create(_ => -149, [ Single.Epsilon ]), // smallest subnormal
		Create(_ => Int32.MinValue, [ 0f ]),
		Create(_ => Int32.MaxValue, [ Single.PositiveInfinity ]),
		Create(_ => Int32.MaxValue, [ Single.NaN ])
	];
}