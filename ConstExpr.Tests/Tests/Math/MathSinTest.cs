using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Sin(double) → FastSin(x) in FastMath mode (polynomial approximation).</summary>
[InheritsTests]
public class MathSinTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(x => System.Math.Sin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastSin(x);")
	];
}