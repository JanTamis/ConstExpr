using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => double.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.MinNumber(a, b);"),
		Create("return 1D;", 1.0, 2.0),
		Create("return -5D;", -5.0, 3.0),
	];
}

/// <summary>double.MinNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMinNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(a => double.MinNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}

/// <summary>float.MinNumber(a, b) — optimizer re-targets to float.MinNumber.</summary>
[InheritsTests]
public class FloatMinNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => float.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.MinNumber(a, b);"),
		Create("return 1F;", 1.0f, 2.0f),
	];
}
