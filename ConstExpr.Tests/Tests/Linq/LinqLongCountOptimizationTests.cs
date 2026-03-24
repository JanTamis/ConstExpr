namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LongCount() optimization - verify that unnecessary operations before LongCount() are removed
/// </summary>
[InheritsTests]
public class LinqLongCountOptimizationTests : BaseTest<Func<int[], long>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).LongCount() => LongCount(predicate)
		var a = x.Where(v => v > 3).LongCount();

		// OrderBy(...).LongCount() => LongCount()
		var b = x.OrderBy(v => v).LongCount();

		// OrderByDescending(...).LongCount() => LongCount()
		var c = x.OrderByDescending(v => v).LongCount();

		// Reverse().LongCount() => LongCount()
		var d = x.Reverse().LongCount();

		// AsEnumerable().LongCount() => LongCount()
		var e = x.AsEnumerable().LongCount();

		// OrderBy().ThenBy().LongCount() => LongCount()
		var f = x.OrderBy(v => v).ThenBy(v => v * 2).LongCount();

		// OrderBy().Where().LongCount() => LongCount(predicate)
		var g = x.OrderBy(v => v).Where(v => v > 2).LongCount();

		// Complex: OrderBy().ThenBy().Reverse().Where().LongCount() => LongCount(predicate)
		var h = x.OrderBy(v => v).ThenBy(v => v * 2).Reverse().Where(v => v < 5).LongCount();

		// Distinct should NOT be optimized (reduces count!)
		var i = x.Distinct().Concat(x).LongCount();

		// Select should be optimized away
		var j = x.Select(v => v * 2).LongCount();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = LongCount_FDQQ2g(x);
			var b = (long)x.Count;
			var c = (long)x.Count;
			var d = (long)x.Count;
			var e = (long)x.Count;
			var f = (long)x.Count;
			var g = LongCount_FDQQ2g(x);
			var h = LongCount_kCjlEw(x);
			var i = LongCount__qaQFQ(x) + (long)x.Count;
			var j = (long)x.Count;
			
			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 37L;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0L;", new int[] { }),
	];
}

