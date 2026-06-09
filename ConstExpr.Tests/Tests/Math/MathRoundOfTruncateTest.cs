using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Round of a Truncate is a no-op — Truncate already yields an integer-valued float.</summary>
[InheritsTests]
public class MathRoundOfTruncateTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Round(System.Math.Truncate(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => double.Truncate(x)),
	];
}