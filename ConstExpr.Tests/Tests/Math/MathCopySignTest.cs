using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCopySignTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((x, y) => System.Math.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return CopySignFastDouble(x, y);"),
		Create((x, _) => double.Abs(x), [ Unknown, 2.0 ]),
		Create((x, _) => -double.Abs(x), [ Unknown, -2.0 ]),
	];
}