using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Round of a Floor is a no-op.</summary>
[InheritsTests]
public class MathRoundOfFloorTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Round(System.Math.Floor(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.Floor(x))
	];
}