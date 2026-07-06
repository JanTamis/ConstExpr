using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFCopySignTest() : BaseTest<Func<float, float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((x, y) => MathF.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCopySignFloat(x, y);"),
		Create((x, _) => Single.Abs(x), [ Unknown, 2f ]),
		Create((x, _) => -Single.Abs(x), [ Unknown, -2f ])
	];
}