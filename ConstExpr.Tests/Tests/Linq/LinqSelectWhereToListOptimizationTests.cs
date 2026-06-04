namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for list.Select(f).Where(p).ToList() => list.FindAll(x => p(f(x)))
/// </summary>
[InheritsTests]
public class LinqSelectWhereToListOptimizationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// list.Select(f).Where(p).ToList() => list.FindAll(x => p(f(x)))
		var a = x.Select(v => v * 2).Where(v => v > 4).ToList().Count;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.FindAll(v => v << 1 > 4).Count),
		Create(_ => 3, [ new List<int> { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new List<int>() ]),
	];
}