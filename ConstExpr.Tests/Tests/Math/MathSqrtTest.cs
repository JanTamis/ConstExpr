using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathSqrtTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Sqrt(x);"),
		Create("return 3D;", 9.0),
		Create("return 2D;", 4.0),
	];
}

/// <summary>Sqrt(x * x) → Abs(x): algebraic identity for pure expressions.</summary>
[InheritsTests]
public class MathSqrtAlgebraicTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x * x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Abs(x);"),
	];
}
