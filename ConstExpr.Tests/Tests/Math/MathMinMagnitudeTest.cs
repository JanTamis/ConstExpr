using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>System.Math.MinMagnitude(double, double) — re-targets to double.MinMagnitude; idempotency; constant folding.</summary>
[InheritsTests]
public class MathMinMagnitudeTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => System.Math.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.MinMagnitude(a, b);"),
		Create("return 1D;", 1.0, -3.0),
		Create("return -2D;", -2.0, 5.0),
	];
}