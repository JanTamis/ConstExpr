using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxMagnitudeNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxMagnitudeNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((a, b) => Double.MaxMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => -3D, [ 1.0, -3.0 ]),
		Create((_, _) => 5D, [ -2.0, 5.0 ])
	];
}