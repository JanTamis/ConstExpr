namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for DefaultIfEmpty() optimization - verify that unnecessary operations before DefaultIfEmpty() are removed
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple DefaultIfEmpty
		var a = x.DefaultIfEmpty().Count();

		// Distinct().DefaultIfEmpty() => DefaultIfEmpty()
		var b = x.Distinct().DefaultIfEmpty().Count();

		// OrderBy(...).DefaultIfEmpty() => DefaultIfEmpty()
		var c = x.OrderBy(v => v).DefaultIfEmpty().Count();

		// OrderByDescending(...).DefaultIfEmpty() => DefaultIfEmpty()
		var d = x.OrderByDescending(v => v).DefaultIfEmpty().Count();

		// Reverse().DefaultIfEmpty() => DefaultIfEmpty()
		var e = x.Reverse().DefaultIfEmpty().Count();

		// AsEnumerable().DefaultIfEmpty() => DefaultIfEmpty()
		var f = x.AsEnumerable().DefaultIfEmpty().Count();

		// ToList().DefaultIfEmpty() => DefaultIfEmpty()
		var g = x.ToList().DefaultIfEmpty().Count();

		// ToArray().DefaultIfEmpty() => DefaultIfEmpty()
		var h = x.ToArray().DefaultIfEmpty().Count();

		// Chained operations: Distinct().OrderBy().Reverse().DefaultIfEmpty() => DefaultIfEmpty()
		var i = x.Distinct().OrderBy(v => v).Reverse().DefaultIfEmpty().Count();

		// DefaultIfEmpty().DefaultIfEmpty() => DefaultIfEmpty() (idempotent)
		var j = x.DefaultIfEmpty().DefaultIfEmpty().Count();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Int32.Max(x.Length, 1);
			var b = Int32.Max(x.Length, 1);
			var c = Int32.Max(x.Length, 1);
			var d = Int32.Max(x.Length, 1);
			var e = Int32.Max(x.Length, 1);
			var f = Int32.Max(x.Length, 1);
			var g = Int32.Max(x.Length, 1);
			var h = Int32.Max(x.Length, 1);
			var i = Int32.Max(x.Length, 1);
			var j = Int32.Max(x.Length, 1);
			
			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 50;", new[] { 1, 2, 3, 4, 5 }), // Non-empty: each DefaultIfEmpty returns 5 elements, so 5*10 = 50
		Create("return 10;", new int[] { }), // Empty: each DefaultIfEmpty returns 1 element (default), so 1*10 = 10
	];
}

/// <summary>
/// Tests for DefaultIfEmpty() with custom default value
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyWithValueTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// DefaultIfEmpty with custom value
		var a = x.DefaultIfEmpty(42).First();

		// Distinct().DefaultIfEmpty(value) => DefaultIfEmpty(value)
		var b = x.Distinct().DefaultIfEmpty(99).First();

		// OrderBy().DefaultIfEmpty(value) => DefaultIfEmpty(value)
		var c = x.OrderBy(v => v).DefaultIfEmpty(77).First();

		// DefaultIfEmpty(10).DefaultIfEmpty(20) => DefaultIfEmpty(20) (last wins)
		var d = x.DefaultIfEmpty(10).DefaultIfEmpty(20).First();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Length > 0 ? x[0] : 42;
			var b = x.Length > 0 ? x[0] : 99;
			var c = x.Length > 0 ? x[0] : 77;
			var d = x.Length > 0 ? x[0] : 20;
			
			return a + b + c + d;
			""", Unknown),
		Create("return 4;", new[] { 1 }), // Non-empty: returns first element (1) four times = 1+1+1+1 = 4
		Create("return 238;", new int[] { }), // Empty: returns default values 42+99+77+20 = 238
	];
}

/// <summary>
/// Tests for DefaultIfEmpty() optimization on List
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Int32.Max(x.Count, 1);
			var b = Int32.Max(x.Count, 1);
			var c = Int32.Max(x.Count, 1);
			var d = Int32.Max(x.Count, 1);
			var e = x.Count > 0 ? x[0] : 100;
			
			return a + b + c + d + e;
			""", Unknown),
		Create("return 21;", new List<int> { 1, 2, 3, 4, 5 }), // Non-empty: 5+5+5+5+1 = 21
		Create("return 104;", new List<int>()), // Empty: 1+1+1+1+100 = 104
	];
}

/// <summary>
/// Tests for DefaultIfEmpty() with complex scenarios
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyComplexTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Multiple chained operations before DefaultIfEmpty
		var a = x.Where(v => v > 0).Distinct().OrderBy(v => v).DefaultIfEmpty(50).Sum();

		// DefaultIfEmpty after Select (Select can create empty collection)
		var b = x.Where(v => v > 100).Select(v => v * 2).DefaultIfEmpty(25).Sum();

		// Nested DefaultIfEmpty with different values
		var c = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).First();
		var d = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).FirstOrDefault();
		
		var e = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).Last();
		var f = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).LastOrDefault();

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Where(v => v > 0).Distinct().Order().DefaultIfEmpty(50).Sum();
			var b = x.Where(v => v > 100).Select(v => v << 1).DefaultIfEmpty(25).Sum();
			var c = x.Length > 0 ? x[0] : 10;
			var d = x.Length > 0 ? x[0] : 10;
			var e = x.Length > 0 ? x[^1] : 10;
			var f = x.Length > 0 ? x[^1] : 10;
			
			return a + b + c + d + e + f;
			""", Unknown),
		Create("return 41;", new[] { 1, 2, 3, 4, 5 }), // a=15 (sum of 1-5), b=25 (empty, default), c=1 (non-empty) = 41
		Create("return 105;", new int[] { }), // a=50 (empty, default), b=25 (empty, default), c=30 (empty, default) = 105
	];
}
