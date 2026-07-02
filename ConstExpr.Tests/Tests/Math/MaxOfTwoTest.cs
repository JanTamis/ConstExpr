using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfTwoTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a > b ? a : b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Int32.Max(a, b)),
		Create((_, _) => 10, [ 5, 10 ]),
		Create((_, _) => 20, [ -10, 20 ]),
		Create((_, _) => 0, [ 0, 0 ])
	];
}