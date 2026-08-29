using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>
///   Regression guard for the RequiredFlags gate: under <see cref="FastMathFlags.Strict" /> the
///   polynomial <c>Math.Sin</c> approximation must NOT be applied — <c>SinFunctionOptimizer</c>
///   keeps the default <c>RequiredFlags = [NoNaN]</c>, so the call is left exactly as written.
///   (Contrast <c>MathSinTest</c>, which runs under <c>FastMathFlags.All</c> and folds to <c>FastSin</c>.)
/// </summary>
[InheritsTests]
public class MathSinStrictNotOptimizedTest() : BaseTest<Func<double, double>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString(x => System.Math.Sin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return System.Math.Sin(x);")
	];
}