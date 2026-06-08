using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathRadiansToDegreesTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => double.RadiansToDegrees(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 0D, [ 0.0 ]),
	];
}