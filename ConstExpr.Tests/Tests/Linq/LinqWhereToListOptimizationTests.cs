namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for list.Where(p).ToList() => list.FindAll(p)
/// </summary>
[InheritsTests]
public class LinqWhereToListOptimizationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// list.Where(p).ToList() => list.FindAll(p)
		var a = x.Where(v => v > 2).ToList().Count;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x.FindAll(v => v > 2).Count),
		CreateFolded(new List<int> { 1, 2, 3, 4, 5 }),
		CreateFolded(new List<int>())
	];
}