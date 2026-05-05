using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMaxNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => double.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MaxNumber(a, b);"),
		Create("return 2D;", 1.0, 2.0),
		Create("return 3D;", -5.0, 3.0),
	];
}

/// <summary>double.MaxNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => double.MaxNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}

/// <summary>float.MaxNumber(a, b) — optimizer re-targets to float.MaxNumber.</summary>
[InheritsTests]
public class FloatMaxNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => float.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.MaxNumber(a, b);"),
		Create("return 2F;", 1.0f, 2.0f),
	];
}
