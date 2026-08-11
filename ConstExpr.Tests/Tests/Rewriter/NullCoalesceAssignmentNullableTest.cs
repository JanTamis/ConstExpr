namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceAssignmentNullableTest : BaseTestWithRandomValues<Func<string?, string, string>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		a ??= b;
		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}