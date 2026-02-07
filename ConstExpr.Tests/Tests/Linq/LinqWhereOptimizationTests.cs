namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Where() optimization - verify constant folding and combining of Where clauses
/// </summary>
[InheritsTests]
public class LinqWhereOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(v => true) - should be removed entirely
		var a = x.Where(v => true).Count();

		// Where(v => false) - should be replaced with Empty
		var b = x.Where(v => false).Count();

		// Consecutive Where calls with same parameter - should combine with &&
		var c = x.Where(v => v > 1).Where(v => v < 5).Count();

		// Consecutive Where calls with different parameters - should still combine
		var d = x.Where(a => a > 0).Where(b => b < 10).Count();

		// Multiple consecutive Where calls - should combine all
		var e = x.Where(v => v > 0).Where(v => v < 10).Where(v => v % 2 == 0).Count();

		// Where(v => true) in chain - should be removed
		var f = x.Where(v => true).Where(v => v > 3).Count();

		// Where(v => false) in chain - result should be empty
		var g = x.Where(v => v > 1).Where(v => false).Count();

		// Complex predicates
		var h = x.Where(v => v > 0 && v < 100).Where(v => v % 2 == 0).Count();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Count();
			var b = 0;
			var c = x.Where(v => (uint)v < 4).Count();
			var d = x.Where(a => (uint)a < 10).Count();
			var e = x.Where(v => (uint)v < 10 && v & 1 == 0).Count();
			var f = x.Where(v => v > 3).Count();
			var g = 0;
			var h = x.Where(v => (uint)v < 100 && v & 1 == 0).Count();
			
			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 17;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
		Create("return 30;", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }),
	];
}
