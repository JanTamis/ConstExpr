namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for CountBy() optimization — verify that redundant materialisation and ordering
/// before CountBy() are stripped, that null-comparer arguments are removed, that
/// Enumerable.Empty&lt;T&gt;() is short-circuited, and that literal Where predicates are folded.
/// </summary>
[InheritsTests]
public class LinqCountByOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().CountBy() => CountBy() (no-op materialisation stripped)
		var a = x.AsEnumerable().CountBy(v => v % 2).Count();
		
		// ToList().CountBy() => CountBy() (materialisation stripped)
		var b = x.ToList().CountBy(v => v % 2).Count();
		
		// ToArray().CountBy() => CountBy() (materialisation stripped)
		var c = x.ToArray().CountBy(v => v % 2).Count();
		
		// OrderBy().CountBy() => CountBy() (ordering doesn't affect key counts)
		var d = x.OrderBy(v => v).CountBy(v => v % 2).Count();
		
		// Enumerable.Empty<T>().CountBy() => Enumerable.Empty<KeyValuePair<TKey,int>>() => Count() = 0
		var e = Enumerable.Empty<int>().CountBy(v => v % 2).Count();
		
		// CountBy(keySelector, null) => CountBy(keySelector) (null comparer removed)
		var f = x.CountBy(v => v % 2, null).Count();

		// Where(v => true).CountBy() => CountBy() (always-true filter stripped)
		var g = x.Where(v => true).CountBy(v => v % 2).Count();

		// Where(v => false).CountBy() => Enumerable.Empty<KeyValuePair<TKey,int>>() => Count() = 0
		var h = x.Where(v => false).CountBy(v => v % 2).Count();
		
		var i = x.CountBy(v => v).Count();

		return a + b + c + d + e + f + g + h + i;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// e (Empty source) and h (Where false) fold to 0 and are pruned from the return sum.
		// v % 2 in key selectors is also optimised to v & 1 by the arithmetic optimizer.
		Create("""
			var a = Count_z525XA(x);
			var b = Count_z525XA(x);
			var c = Count_z525XA(x);
			var d = Count_z525XA(x);
			var f = Count_z525XA(x);
			var g = Count_z525XA(x);
			var i = x.Length;
			
			return a + b + c + d + f + g + i;
			"""),
		Create("return 17;", new[] { 1, 2, 3, 4, 5 }),  // 6 calls × 2 keys each; e=0, h=0
		Create("return 0;",  new int[] { }),            // all groups empty
	];
}

