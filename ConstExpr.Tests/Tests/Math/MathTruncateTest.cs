using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathTruncateTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Truncate(x);"),
		Create("return 3D;", 3.7),
		Create("return -3D;", -3.2),
	];
}

/// <summary>Truncate(-x) → -(Truncate(x)): moves negation outside.</summary>
[InheritsTests]
public class MathTruncateNegationTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(-x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -Double.Truncate(x);"),
	];
}

/// <summary>Truncate(Truncate(x)) → Truncate(x): idempotency.</summary>
[InheritsTests]
public class MathTruncateIdempotencyTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(System.Math.Truncate(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Truncate(x);"),
	];
}

/// <summary>Truncate(Floor(x)) → Floor(x): Floor already returns integer-valued float.</summary>
[InheritsTests]
public class MathTruncateOfFloorTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(System.Math.Floor(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Floor(x);"),
	];
}
