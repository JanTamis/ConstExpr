using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Exp2(double) → FastExp2(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathExp2Test() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => double.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);"),
		Create("return 1D;", 0.0),
		Create("return 8D;", 3.0),
	];
}

/// <summary>MathF.Exp2(float) → FastExp2(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFExp2Test() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => float.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);"),
	];
}



