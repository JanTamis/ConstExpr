namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   <c>Add</c> can reallocate the list's backing array, which would leave the hoisted reference
///   pointing at the old one. Anything outside the read-only allowlist must block the rewrite.
/// </summary>
/// <remarks>
///   Not a random-value test: the folded-random harness can only express <c>return &lt;value&gt;;</c>, which
///   cannot describe a method that mutates its (caller-owned) parameter - the <c>Add</c> must stay in the output.
/// </remarks>
[InheritsTests]
public class BoundsCheckEliminationListMutatedTests : BaseTest<Func<List<int>, int, int>>
{
	public override string TestMethod => GetString((values, i) =>
	{
		values.Add(i);

		return values[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create("""
			values.Add(1);

			return 5;
			""", new List<int> { 5 }, 1)
	];
}