using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Truncate(Floor(x)) → Floor(x): Floor already returns integer-valued float.</summary>
[InheritsTests]
public class MathTruncateOfFloorTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(System.Math.Floor(x)));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => double.Floor(x)),
	];
}