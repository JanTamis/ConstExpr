using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCopySignTest() : BaseTest<Func<double, double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((x, y) => System.Math.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCopySignDouble(x, y);"),
		Create((x, _) => Double.Abs(x), [ Unknown, 2.0 ]),
		Create((x, _) => -Double.Abs(x), [ Unknown, -2.0 ])
	];
}