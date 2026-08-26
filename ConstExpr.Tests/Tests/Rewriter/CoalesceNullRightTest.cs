namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for null-coalescing right-null fold: x ?? null => x.</summary>
[InheritsTests]
public class CoalesceNullRightTest : BaseTestWithRandomValues<Func<string?, string?>>
{
	public override string TestMethod => GetString(x => x ?? null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x)
	];
}