namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NullCoalesceAssignmentExpressionNonNullableTest : BaseTestWithRandomValues<Func<string, string, string>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var r = a ??= b;
		return r;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
	];
}