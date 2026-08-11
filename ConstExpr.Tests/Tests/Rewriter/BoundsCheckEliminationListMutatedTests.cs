namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   <c>Add</c> can reallocate the list's backing array, which would leave the hoisted reference
///   pointing at the old one. Anything outside the read-only allowlist must block the rewrite.
/// </summary>
[InheritsTests]
public class BoundsCheckEliminationListMutatedTests : BaseTestWithRandomValues<Func<List<int>, int, int>>
{

	public override string TestMethod => GetString((values, i) =>
	{
		values.Add(i);

		return values[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault()
	];
}