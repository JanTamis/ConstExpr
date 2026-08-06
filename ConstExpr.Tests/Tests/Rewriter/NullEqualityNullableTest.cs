namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullEqualityNullableTest : BaseTest<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => s == null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => true, [ null ]),
		Create(_ => false, [ "hello" ])
	];
}