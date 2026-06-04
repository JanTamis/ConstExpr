using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.MinMagnitude(float, float) — re-targets to float.MinMagnitude.</summary>
[InheritsTests]
public class MathFMinMagnitudeTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => MathF.MinMagnitude(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => Single.MinMagnitude(a, b)),
		Create((_, _) => 1F, [ 1.0f, -3.0f ]),
	];
}