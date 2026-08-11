namespace ConstExpr.Tests.Linq;

[InheritsTests]
public class LinqByOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		var a = x.DistinctBy(v => v % 2).Count();
		var b = x.ExceptBy([ 2, 4, 6 ], v => v).Count();
		var c = x.IntersectBy([ 2, 4, 6 ], v => v).Count();
		var d = x.UnionBy([ 2, 4, 6 ], v => v).Count();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_BgXwWg(x) + Count_A_x9WQ(x, v) + Count_GdwhZA(x, v) + Count_0yIQSg(x);", Unknown),
	];
}