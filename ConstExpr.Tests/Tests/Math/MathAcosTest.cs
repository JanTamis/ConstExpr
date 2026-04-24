using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Acos(double) → FastAcos(x) in FastMath mode.</summary>
[InheritsTests]
public class MathAcosTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Acos(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcos(x);"),
	];
}

/// <summary>MathF.Acos(float) → FastAcos(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAcosTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Acos(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcos(x);"),
	];
}

