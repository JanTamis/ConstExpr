using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>double.MaxNumber(a, a) — idempotency optimization: returns a.</summary>
[InheritsTests]
public class MathMaxNumberIdempotentTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(a => Double.MaxNumber(a, a));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(a => a)
	];
}