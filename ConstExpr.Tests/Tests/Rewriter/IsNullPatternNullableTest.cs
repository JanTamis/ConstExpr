namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class IsNullPatternNullableTest : BaseTestWithRandomValues<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => s is null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => s == null),
	];
}