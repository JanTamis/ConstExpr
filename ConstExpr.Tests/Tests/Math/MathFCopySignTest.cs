namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFCopySignTest : BaseTest<Func<float, float, float>>
{
	public override string TestMethod => GetString((x, y) => MathF.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCopySign<float, int>(x, y);"),
		Create((x, _) => Single.Abs(x), [ Unknown, 2f ]),
		Create((x, _) => -Single.Abs(x), [ Unknown, -2f ])
	];
}