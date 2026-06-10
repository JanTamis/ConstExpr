using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>float.MinMagnitudeNumber(a, b) — optimizer re-targets to float.MinMagnitudeNumber.</summary>
[InheritsTests]
public class FloatMinMagnitudeNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => float.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 1F, [ 1.0f, -3.0f ]),
	];
}