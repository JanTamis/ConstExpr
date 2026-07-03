using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Truncate(Truncate(x)) → Truncate(x): idempotency.</summary>
[InheritsTests]
public class MathTruncateIdempotencyTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(System.Math.Truncate(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Truncate(x))
	];
}