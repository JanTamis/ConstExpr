namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Any() optimization - verify that unnecessary operations before Any() are removed
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Any() => Any(predicate)
		var a = x.Where(v => v > 3).Any() ? 1 : 0;

		// Select(...).Any() => Any()
		var b = x.Select(v => v * 2).Any() ? 1 : 0;

		// Distinct().Any() => Any()
		var c = x.Distinct().Any() ? 1 : 0;

		// OrderBy(...).Any() => Any()
		var d = x.OrderBy(v => v).Any() ? 1 : 0;

		// OrderByDescending(...).Any() => Any()
		var e = x.OrderByDescending(v => v).Any() ? 1 : 0;

		// Reverse().Any() => Any()
		var f = x.Reverse().Any() ? 1 : 0;

		// AsEnumerable().Any() => Any()
		var g = x.AsEnumerable().Any() ? 1 : 0;

		// ToList().Any() => Any()
		var h = x.ToList().Any() ? 1 : 0;

		// ToArray().Any() => Any()
		var i = x.ToArray().Any() ? 1 : 0;

		// Where filters everything out
		var j = x.Where(v => v > 100).Any() ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Any(v => v > 3) ? 1 : 0;
			var b = x.Any() ? 1 : 0;
			var c = x.Any() ? 1 : 0;
			var d = x.Any() ? 1 : 0;
			var e = x.Any() ? 1 : 0;
			var f = x.Any() ? 1 : 0;
			var g = x.Any() ? 1 : 0;
			var h = x.Any() ? 1 : 0;
			var i = x.Any() ? 1 : 0;
			var j = x.Any(v => v > 100) ? 1 : 0;
			
			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 9;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}
