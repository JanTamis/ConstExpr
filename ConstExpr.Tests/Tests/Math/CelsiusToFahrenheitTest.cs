using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class CelsiusToFahrenheitTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(celsius => celsius * 9 / 5 + 32);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(celsius => Double.MultiplyAddEstimate(celsius, 1.8, 32D)),
		Create(_ => 32D, [ 0.0 ]),
		Create(_ => 212D, [ 100.0 ]),
		Create(_ => 77D, [ 25.0 ])
	];
}