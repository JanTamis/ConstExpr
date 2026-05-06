using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

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