namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for null-coalescing left-null fold: null ?? x => x.</summary>
[InheritsTests]
public class CoalesceNullLeftTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(x => null ?? x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x)
	];
}