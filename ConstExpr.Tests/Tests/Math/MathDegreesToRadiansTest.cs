using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathDegreesToRadiansTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => double.DegreesToRadians(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => double.DegreesToRadians(x)),
		Create(_ => 0D, [ 0.0 ]),
	];
}