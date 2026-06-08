using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathBitIncrementTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.BitIncrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitIncrement(x);"),
		Create(_ => 2D, [ 1.9999999999999998 ]),
	];
}