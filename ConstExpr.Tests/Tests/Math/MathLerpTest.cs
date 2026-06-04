using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Lerp(double, double, double) → FastLerp(a, b, t) in FastMath mode.</summary>
[InheritsTests]
public class MathLerpTest() : BaseTest<Func<double, double, double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b, t) => double.Lerp(a, b, t));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLerp(a, b, t);"),
		Create((_, _, _) => 5D, [ 0.0, 10.0, 0.5 ]),
		Create((_, _, _) => 0D, [ 0.0, 10.0, 0.0 ]),
		Create((_, _, _) => 10D, [ 0.0, 10.0, 1.0 ]),
	];
}