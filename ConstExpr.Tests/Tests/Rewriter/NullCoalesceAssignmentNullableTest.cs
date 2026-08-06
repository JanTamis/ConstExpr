namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceAssignmentNullableTest : BaseTest<Func<string?, string, string>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		a ??= b;
		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("""
			a ??= "world";

			return a;
			""", "hello", "world"),
		Create("""
			a ??= "world";

			return a;
			""", null, "world")
	];
}