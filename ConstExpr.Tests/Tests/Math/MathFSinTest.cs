using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Sin(float) → FastSin(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFSinTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.Sin(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastSin(x);"),
	];
}