namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for DefaultIfEmpty() optimization on List
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple DefaultIfEmpty on List
		var a = x.DefaultIfEmpty().Count();

		// Distinct().DefaultIfEmpty() => DefaultIfEmpty()
		var b = x.Distinct().DefaultIfEmpty().Count();

		// OrderBy().DefaultIfEmpty() => DefaultIfEmpty()
		var c = x.OrderBy(v => v).DefaultIfEmpty().Count();

		// DefaultIfEmpty().DefaultIfEmpty() => DefaultIfEmpty()
		var d = x.DefaultIfEmpty().DefaultIfEmpty().Count();

		// DefaultIfEmpty with value
		var e = x.DefaultIfEmpty(100).First();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var xCount = x.Count;

			return Int32.Max(xCount, 1) * 3 + Int32.Max(Count_w6J_9Q(x), 1) + (xCount > 0 ? x[0] : 100);
			"""),
		CreateFolded(new List<int> { 1, 2, 3, 4, 5 }), // Non-empty: 5+5+5+5+1 = 21
		CreateFolded(new List<int>()) // Empty: 1+1+1+1+100 = 104
	];
}