using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MinNumber(a, b) — optimizer re-targets and handles idempotency.</summary>
[InheritsTests]
public class MathMinNumberTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => double.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 1D;", 1.0, 2.0),
		Create("return -5D;", -5.0, 3.0),
	];
}