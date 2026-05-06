using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Cbrt(float) → FastCbrt(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFCbrtTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Cbrt(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastCbrt(x);"),
	];
}