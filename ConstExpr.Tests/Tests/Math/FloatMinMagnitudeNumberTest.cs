namespace ConstExpr.Tests.Math;

/// <summary>float.MinMagnitudeNumber(a, b) — optimizer re-targets to float.MinMagnitudeNumber.</summary>
[InheritsTests]
public class FloatMinMagnitudeNumberTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => Single.MinMagnitudeNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}