using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Log(double) → FastLog(x) in FastMath mode.</summary>
[InheritsTests]
public class MathLogTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Log(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog(x);"),
	];
}

/// <summary>MathF.Log(float) → FastLog(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLogTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Log(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog(x);"),
	];
}
