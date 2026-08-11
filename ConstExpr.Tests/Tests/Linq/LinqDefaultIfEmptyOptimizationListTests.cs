namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for DefaultIfEmpty() optimization on List
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyOptimizationListTests : BaseTestWithRandomValues<Func<List<int>, int>>
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
	];
}