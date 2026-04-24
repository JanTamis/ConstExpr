using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Exp(double) → FastExp(x) in FastMath mode.</summary>
[InheritsTests]
public class MathExpTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Exp(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp(x);"),
	];
}

/// <summary>MathF.Exp(float) → FastExp(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFExpTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Exp(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp(x);"),
	];
}
