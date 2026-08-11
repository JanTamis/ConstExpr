namespace ConstExpr.Tests.Math;

/// <summary>float.MinNumber(a, b) — optimizer re-targets to float.MinNumber.</summary>
[InheritsTests]
public class FloatMinNumberTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => Single.MinNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}