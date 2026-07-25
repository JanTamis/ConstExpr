namespace ConstExpr.Tests.Math;

/// <summary>MathF.Acosh(float) -> FastAcosh(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFAcoshTest : BaseTest<Func<float, float>>
{
	public override string TestMethod => GetString(x => MathF.Acosh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcosh(x);")
	];
}