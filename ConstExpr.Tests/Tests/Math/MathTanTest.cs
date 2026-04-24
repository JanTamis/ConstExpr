using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Tan(double) → FastTan(x) in FastMath mode, with algebraic constant folding.</summary>
[InheritsTests]
public class MathTanTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);"),
		Create("return 0D;", 0.0),
	];
}

/// <summary>MathF.Tan(float) → FastTan(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFTanTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);"),
	];
}

