namespace ConstExpr.Tests.Math;

/// <summary>float.MaxNumber(a, b) — optimizer re-targets to float.MaxNumber.</summary>
[InheritsTests]
public class FloatMaxNumberTest : BaseTestWithRandomValues<Func<float, float, float>>
{
	public override string TestMethod => GetString((a, b) => Single.MaxNumber(a, b));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}