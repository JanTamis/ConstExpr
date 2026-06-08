using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DivideByPowerOfTwoTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((n, power) => n >> power);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => 2, [ 10, 2 ]),
		Create((_, _) => 0, [ 1, 5 ]),
		Create((_, _) => 4, [ 128, 5 ])
	];
}