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
		// a is KNOWN non-null here, so it folds straight through to a's own value.
		CreateFolded("hello", "world"),
		Create("""
			a ??= "world";

			return a;
			""", null, "world")
	];
}