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
		var i = x.Distinct().LongCount();

		// Select should be optimized away
		var j = x.Select(v => v * 2).LongCount();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.LongCount(v => v > 3);
			var b = (long)x.Length;
			var c = (long)x.Length;
			var d = (long)x.Length;
			var e = (long)x.Length;
			var f = (long)x.Length;
			var g = x.LongCount(v => v > 2);
			var h = x.LongCount(v => v < 5);
			var i = x.Distinct().LongCount();
			var j = (long)x.Length;
			
			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 37L;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0L;", new int[] { }),
	];
}

