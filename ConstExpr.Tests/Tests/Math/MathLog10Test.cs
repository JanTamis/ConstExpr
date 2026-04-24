using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Log10(double) → FastLog10(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathLog10Test() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Log10(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog10(x);"),
		Create("return 1D;", 10.0),
		Create("return 2D;", 100.0),
	];
}

/// <summary>MathF.Log10(float) → FastLog10(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLog10Test() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Log10(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog10(x);"),
	];
}

