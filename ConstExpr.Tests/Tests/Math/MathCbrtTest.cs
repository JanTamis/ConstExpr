using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Cbrt(double) → FastCbrt(x) in FastMath mode.</summary>
[InheritsTests]
public class MathCbrtTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Cbrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCbrt(x);"),
		Create("return 2D;", 8.0),
		Create("return 3D;", 27.0),
	];
}