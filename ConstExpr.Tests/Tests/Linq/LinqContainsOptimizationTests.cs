namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Contains() optimization - verify that unnecessary operations before Contains() are removed
/// and that Contains is optimized for specific collection types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains
		var a = x.Contains(3) ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains(3) ? 1 : 0;

		// OrderBy(...).Contains() => Contains()
		var c = x.OrderBy(v => v).Contains(3) ? 1 : 0;

		// OrderByDescending(...).Contains() => Contains()
		var d = x.OrderByDescending(v => v).Contains(3) ? 1 : 0;

		// Reverse().Contains() => Contains()
		var e = x.Reverse().Contains(3) ? 1 : 0;

		// AsEnumerable().Contains() => Contains()
		var f = x.AsEnumerable().Contains(3) ? 1 : 0;

		// ToList().Contains() => Contains()
		var g = x.ToList().Contains(3) ? 1 : 0;

		// ToArray().Contains() => Contains()
		var h = x.ToArray().Contains(3) ? 1 : 0;

		// Chained operations: Distinct().OrderBy().Reverse().Contains() => Contains()
		var i = x.Distinct().OrderBy(v => v).Reverse().Contains(3) ? 1 : 0;

		// Select(...).Contains() => Any(...)
		var j = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Any(...)
		var k = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var l = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var b = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var c = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var d = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var e = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var f = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var g = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var h = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var i = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var j = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var k = Array.IndexOf(x, 3) >= 0 ? 1 : 0;
			var l = Array.IndexOf(x, 100) >= 0 ? 1 : 0;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 11;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
		Create("return 0;", new[] { 1, 2, 4, 5, 6 }), // No 3, all tests fail
	];
}

/// <summary>
/// Tests for Contains() optimization on List - verify that Contains is optimized for List type
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains
		var a = x.Contains(3) ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains(3) ? 1 : 0;

		// OrderBy(...).Contains() => Contains()
		var c = x.OrderBy(v => v).Contains(3) ? 1 : 0;

		// Reverse().Contains() => Contains()
		var d = x.AsEnumerable().Reverse().Contains(3) ? 1 : 0;

		// Select(...).Contains() => Exists(...)
		var e = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Exists(...)
		var f = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var g = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Contains(3) ? 1 : 0;
			var b = x.Contains(3) ? 1 : 0;
			var c = x.Contains(3) ? 1 : 0;
			var d = x.Contains(3) ? 1 : 0;
			var e = x.Contains(3) ? 1 : 0;
			var f = x.Contains(3) ? 1 : 0;
			var g = x.Contains(100) ? 1 : 0;
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 6;", new List<int> { 1, 2, 3, 4, 5 }),
		Create("return 0;", new List<int>()),
		Create("return 0;", new List<int> { 1, 2, 4, 5, 6 }), // No 3, all tests fail
	];
}

/// <summary>
/// Tests for Contains() with string values - verify optimization works with different types
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationStringTests : BaseTest<Func<string[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains with string
		var a = x.Contains("hello") ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains("world") ? 1 : 0;

		// Select(...).Contains() with string transformation
		var c = x.Select(v => v.ToUpper()).Contains("HELLO") ? 1 : 0;

		// Where(...).Contains()
		var d = x.Where(v => v.Length > 3).Contains("hello") ? 1 : 0;

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Array.IndexOf(x, "hello") >= 0 ? 1 : 0;
			var b = Array.IndexOf(x, "world") >= 0 ? 1 : 0;
			var c = Array.Exists(x, v => String.Equals(v, "HELLO", StringComparer.CurrentCultureIgnoreCase)) ? 1 : 0;
			var d = Array.IndexOf(x, "hello") >= 0 ? 1 : 0;
			
			return a + b + c + d;
			""", Unknown),
		Create("return 4;", "hello", "world", "foo"),
		Create("return 0;"),
		Create("return 1;", "hi", "world", "test"), // Only b matches ("world")
	];
}

/// <summary>
/// Tests for Contains() optimization with complex lambda expressions
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationComplexTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Multiple chained operations before Contains
		var a = x.Where(v => v > 0).Distinct().OrderBy(v => v).Contains(5) ? 1 : 0;

		// Select with more complex expression
		var b = x.Select(v => v + 10).Contains(15) ? 1 : 0;

		// Where with complex predicate
		var c = x.Where(v => v % 2 == 0).Contains(4) ? 1 : 0;

		// Nested operations
		var d = x.Distinct().Where(v => v < 10).Contains(5) ? 1 : 0;

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Contains(5); ? 1 : 0;
			var b = x.Contains(5); ? 1 : 0;
			var c = x.Contains(4); ? 1 : 0;
			var d = x.Contains(5); ? 1 : 0;
			
			return a + b + c + d;
			""", Unknown),
		Create("return 4;", new[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
		Create("return 0;", new int[] { }),
		Create("return 3;", new[] { 5, 10, 15 }), // a=1 (5>0 && 5==5), b=1 (5+10==15), c=0 (no 4), d=1 (5<10 && 5==5)
	];
}
