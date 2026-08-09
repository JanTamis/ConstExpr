namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Specifically exercises the ConstExprPartialRewriter.Patterns.cs one-line fix: "is not X" used to
///   return an unvisited NotEqualsExpression, so it never reached the new fold. Without that fix this
///   test fails while IsNullPatternNonNullableTest ("is null") passes.
/// </summary>
[InheritsTests]
public class IsNotNullPatternNonNullableTest : BaseTest<Func<string, bool>>
{
	public override string TestMethod => GetString(s => s is not null);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => true),
		CreateFolded("hello")
	];
}