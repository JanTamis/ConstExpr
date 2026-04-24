using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathRoundTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Round(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Round(x);"),
		Create("return 4D;", 3.7),
		Create("return 3D;", 3.2),
	];
}

/// <summary>Round of a Truncate is a no-op — Truncate already yields an integer-valued float.</summary>
[InheritsTests]
public class MathRoundOfTruncateTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Round(System.Math.Truncate(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Truncate(x);"),
	];
}

/// <summary>Round of a Floor is a no-op.</summary>
[InheritsTests]
public class MathRoundOfFloorTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Round(System.Math.Floor(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Floor(x);"),
	];
}
