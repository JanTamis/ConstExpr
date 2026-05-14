using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Tests for Pow algebraic strategies: literal base transformations.</summary>
[InheritsTests]
public class PowTwoToExpTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n => System.Math.Pow(2.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Exp2(n);"),
		Create("return 8D;", 3.0),
		Create("return 1D;", 0.0)
	];
}

[InheritsTests]
public class PowTenToExpTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n => System.Math.Pow(10.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Exp10(n);"),
		Create("return 1000D;", 3.0),
		Create("return 1D;", 0.0)
	];
}

[InheritsTests]
public class PowNegHalfExpTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Pow(x, -0.5));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.ReciprocalSqrtEstimate(x);"),
		Create("return 0.5D;", 4.0),
		Create("return 1D;", 1.0)
	];
}