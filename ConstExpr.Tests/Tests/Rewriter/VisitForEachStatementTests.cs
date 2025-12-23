namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitForEachStatement - foreach loop unrolling
/// </summary>
[InheritsTests]
public class VisitForEachStatementTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(items =>
	{
		var sum = 0;

		foreach (var i in items)
		{
			sum += i;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 6;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
		Create("return 15;", new[] { 4, 5, 6 })
	];
}