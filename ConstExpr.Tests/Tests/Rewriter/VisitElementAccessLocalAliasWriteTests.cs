namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A write through a single-use alias of a local array (<c>var a = b; a[0] = n;</c>) is a write to that
///   array: the alias declaration is inlined away, so the write has to reach <c>b</c>'s tracked value
///   instead of being dropped, and later reads of <c>b</c> fold against it.
/// </summary>
[InheritsTests]
public class VisitElementAccessLocalAliasWriteTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var b = new[] { 1, 2 };
		var a = b;

		a[0] = n;

		return b[0] + b[1];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 9;", 7)
	];
}
