namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceNullableTest : BaseTest<Func<string?, string, string>>
{
	public override string TestMethod => GetString((a, b) => a ?? b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => "hello", [ "hello", "world" ]),
		Create((_, _) => "world", [ null, "world" ])
	];
}