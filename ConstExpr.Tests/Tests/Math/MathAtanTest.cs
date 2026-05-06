using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Atan(double) → FastAtan(x) in FastMath mode, with algebraic constant folding.</summary>
[InheritsTests]
public class MathAtanTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Atan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtan(x);"),
		Create("return 0D;", 0.0),
	];
}