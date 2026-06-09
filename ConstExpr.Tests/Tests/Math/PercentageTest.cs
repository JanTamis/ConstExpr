using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PercentageTest() : BaseTest<Func<double, double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((value, percentage) => value * percentage / 100);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((value, percentage) => value * percentage * 0.01),
		Create((_, _) => 25D, [ 100.0, 25.0 ]),
		Create((_, _) => 0D, [ 50.0, 0.0 ]),
		Create((_, _) => 7.5D, [ 50.0, 15.0 ])
	];
}