using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MinOfTwoTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a < b ? a : b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => a < b ? a : b),
		Create((_, _) => 5, [ 5, 10 ]),
		Create((_, _) => -10, [ -10, 20 ]),
		Create((_, _) => 0, [ 0, 0 ])
	];
}