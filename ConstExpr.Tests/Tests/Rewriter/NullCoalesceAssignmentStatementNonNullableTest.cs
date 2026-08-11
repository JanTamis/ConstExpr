namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   a ??= b; used as a standalone statement. Regression test for the CS0201 hazard: folding just the
///   expression to a bare identifier ("a;") isn't legal C#, so the whole statement must become a no-op
///   instead. Also asserts the generated body no longer references b.
/// </summary>
[InheritsTests]
public class NullCoalesceAssignmentStatementNonNullableTest : BaseTestWithRandomValues<Func<string, string, string>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		a ??= b;
		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a;"),
		Create("return \"hello\";", "hello", "world")
	];
}