namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for null-coalescing non-null-literal-left fold: "a" ?? x => "a".</summary>
[InheritsTests]
public class CoalesceNonNullLiteralLeftTest : BaseTestWithRandomValues<Func<string, string>>
{
	public override string TestMethod => GetString(x => "a" ?? x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => "a")
	];
}