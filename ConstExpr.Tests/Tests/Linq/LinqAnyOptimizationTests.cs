namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Any() optimization - verify that unnecessary operations before Any() are removed
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Any() => Array.Exists(...) for arrays
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

		// Where filters everything out => Array.Exists(...) for arrays
		var j = x.Where(v => v == 100).Any() ? 1 : 0;

		// Should be optimized to Contains
		var k = x.Any(v => v == 2) ? 1 : 0;

		// Direct Any() on array => x.Length > 0
		var l = x.Any() ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Array.Exists(x, v => v > 3) ? 1 : 0;
			var b = x.Length > 0 ? 1 : 0;
			var c = x.Length > 0 ? 1 : 0;
			var d = x.Length > 0 ? 1 : 0;
			var e = x.Length > 0 ? 1 : 0;
			var f = x.Length > 0 ? 1 : 0;
			var g = x.Length > 0 ? 1 : 0;
			var h = x.Length > 0 ? 1 : 0;
			var i = x.Length > 0 ? 1 : 0;
			var j = x.Contains(100) ? 1 : 0;
			var k = x.Contains(2) ? 1 : 0;
			var l = x.Length > 0 ? 1 : 0;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 11;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}

/// <summary>
/// Tests for Any() optimization on List - verify that List.Where().Any() is optimized to List.Exists()
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// List.Where(...).Any() => List.Exists(...)
		var a = x.Where(v => v > 3).Any() ? 1 : 0;

		// List.Select(...).Any() => List.Any()
		var b = x.Select(v => v * 2).Any() ? 1 : 0;

		// List.OrderBy(...).Any() => List.Any()
		var c = x.OrderBy(v => v).Any() ? 1 : 0;

		// List.Where filters everything out => List.Exists(...)
		var d = x.Where(v => v > 100).Any() ? 1 : 0;

		// Should be optimized to Contains
		var e = x.Any(v => v == 2) ? 1 : 0;

		// Direct Any() on list => x.Count > 0
		var f = x.Any() ? 1 : 0;

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Exists(v => v > 3) ? 1 : 0;
			var b = x.Count > 0 ? 1 : 0;
			var c = x.Count > 0 ? 1 : 0;
			var d = x.Exists(v => v > 100) ? 1 : 0;
			var e = x.Contains(2) ? 1 : 0;
			var f = x.Count > 0 ? 1 : 0;
			
			return a + b + c + d + e + f;
			""", Unknown),
		Create("return 5;", new List<int> { 1, 2, 3, 4, 5 }),
		Create("return 0;", new List<int>()),
	];
}

