using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>float.MinNumber(a, b) — optimizer re-targets to float.MinNumber.</summary>
[InheritsTests]
public class FloatMinNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => float.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => 1F, [ 1.0f, 2.0f ]),
	];
}