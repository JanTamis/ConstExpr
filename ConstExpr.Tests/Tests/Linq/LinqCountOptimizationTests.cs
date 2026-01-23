namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Count() optimization - verify that unnecessary operations before Count() are removed
/// </summary>
[InheritsTests]
public class LinqCountOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Count() => Count(predicate)
		var a = x.Where(v => v > 3).Count();

		// OrderBy(...).Count() => Count()
		var b = x.OrderBy(v => v).Count();

		// OrderByDescending(...).Count() => Count()
		var c = x.OrderByDescending(v => v).Count();

		// Reverse().Count() => Count()
		var d = x.Reverse().Count();

		// AsEnumerable().Count() => Count()
		var e = x.AsEnumerable().Count();

		// OrderBy().ThenBy().Count() => Count()
		var f = x.OrderBy(v => v).ThenBy(v => v * 2).Count();

		// OrderBy().Where().Count() => Count(predicate)
		var g = x.OrderBy(v => v).Where(v => v > 2).Count();

		// Complex: OrderBy().ThenBy().Reverse().Where().Count() => Count(predicate)
		var h = x.OrderBy(v => v).ThenBy(v => v * 2).Reverse().Where(v => v < 5).Count();

		// Distinct should NOT be optimized (reduces count!)
		var i = x.Distinct().Count();

		// Select should NOT be optimized (might affect count)
		var j = x.Select(v => v * 2).Count();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Count(v => v > 3);
			var b = x.Count();
			var c = x.Count();
			var d = x.Count();
			var e = x.Count();
			var f = x.Count();
			var g = x.Count(v => v > 2);
			var h = x.Count(v => v < 5);
			var i = x.Distinct().Count();
			var j = x.Select(v => v * 2).Count();
			
			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 37;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}
