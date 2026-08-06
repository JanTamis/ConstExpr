namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class ConditionalAccessNullableTest : BaseTest<Func<string?, string?>>
{
	public override string TestMethod => GetString(s => s?.Trim());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}