namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitIfStatement - merging a nested if-without-else into its parent.
///   `if (A) { if (B) S }` only runs S when A AND B are both true, so the merged
///   condition must be `A && B`, not `A || B`.
/// </summary>
[InheritsTests]
public class VisitIfStatementNestedMergeTests : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((a, b) =>
	{
		var c = 0;

		if (a > 0)
		{
			if (b > 0)
			{
				c = 1;
			}
		}

		return c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => a > 0 && b > 0 ? 1 : 0)
	];
}