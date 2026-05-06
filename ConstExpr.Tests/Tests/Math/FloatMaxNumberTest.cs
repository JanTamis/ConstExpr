using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>float.MaxNumber(a, b) — optimizer re-targets to float.MaxNumber.</summary>
[InheritsTests]
public class FloatMaxNumberTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => float.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return 2F;", 1.0f, 2.0f),
	];
}