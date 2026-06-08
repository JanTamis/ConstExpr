using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class HypotenuseTest() : BaseTest<Func<int, int, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => System.Math.Sqrt(a * a + b * b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Double.Sqrt(a * a + b * b)),
		Create((_, _) => 5D, [ 3, 4 ]),
		Create((_, _) => 13D, [ 5, 12 ]),
		Create((_, _) => 10D, [ 0, 10 ])
	];
}