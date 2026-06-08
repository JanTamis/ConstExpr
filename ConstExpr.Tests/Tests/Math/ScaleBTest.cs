using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ScaleBTest() : BaseTest<Func<double, int, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((x, n) => System.Math.ScaleB(x, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastScaleB(x, n);"), // Unknown args → emit fast helper
		Create((x, _) => x, [ Unknown, 0 ]), // ScaleB(x, 0) = x (2^0 = 1)
		Create((_, _) => 0D, [ 0.0, Unknown ]), // ScaleB(0, n) = 0
		Create((_, _) => 16D, [ 2.0, 3 ]) // ScaleB(2.0, 3) = 2.0 * 2^3 = 16.0
	];
}