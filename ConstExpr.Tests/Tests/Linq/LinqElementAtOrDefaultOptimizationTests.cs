namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ElementAtOrDefault() optimization - verify that unnecessary operations before ElementAtOrDefault() are removed
/// Note: ElementAtOrDefault cannot be optimized to direct indexing because it returns default instead of throwing
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAtOrDefault
		var a = x.ElementAtOrDefault(0);

		// AsEnumerable().ElementAtOrDefault() => ElementAtOrDefault()
		var b = x.AsEnumerable().ElementAtOrDefault(1);

		// ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var c = x.ToList().ElementAtOrDefault(2);

		// ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var d = x.ToArray().ElementAtOrDefault(0);

		// AsEnumerable().ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var e = x.AsEnumerable().ToList().ElementAtOrDefault(1);

		// Complex chain: AsEnumerable().ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var f = x.AsEnumerable().ToArray().ElementAtOrDefault(2);

		// ElementAtOrDefault with index out of bounds (should return 0 for int)
		var g = x.ElementAtOrDefault(10);

		// ElementAtOrDefault with valid index
		var h = x.ElementAtOrDefault(3);

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.FirstOrDefault();
			var b = x.ElementAtOrDefault(1);
			var c = x.ElementAtOrDefault(2);
			var d = x.FirstOrDefault();
			var e = x.ElementAtOrDefault(1);
			var f = x.ElementAtOrDefault(2);
			var g = x.ElementAtOrDefault(10);
			var h = x.ElementAtOrDefault(3);
			
			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 16;", new[] { 1, 2, 3, 4, 5 }), // 1 + 2 + 3 + 1 + 2 + 3 + 0 + 4 = 16
		Create("return 0;", new int[] { }), // All return 0 (default)
		Create("return 0;", new[] { 0, 0, 0, 0, 0 }),
	];
}

/// <summary>
/// Tests for ElementAtOrDefault() optimization on List
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAtOrDefault on List
		var a = x.ElementAtOrDefault(0);

		// AsEnumerable().ElementAtOrDefault() => ElementAtOrDefault()
		var b = x.AsEnumerable().ElementAtOrDefault(1);

		// ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var c = x.ToArray().ElementAtOrDefault(0);

		// ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var d = x.ToList().ElementAtOrDefault(1);

		// Out of bounds
		var e = x.ElementAtOrDefault(10);

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.FirstOrDefault();
			var b = x.ElementAtOrDefault(1);
			var c = x.FirstOrDefault();
			var d = x.ElementAtOrDefault(1);
			var e = x.ElementAtOrDefault(10);
			
			return a + b + c + d + e;
			""", Unknown),
		Create("return 6;", new List<int> { 1, 2, 3, 4, 5 }), // 1 + 2 + 1 + 2 + 0 = 6
		Create("return 0;", new List<int>()), // All return 0 (default)
	];
}

/// <summary>
/// Tests for ElementAtOrDefault() optimization with Skip - verify that Skip is properly optimized
/// When Skip results in ElementAtOrDefault(0), it should further optimize to FirstOrDefault()
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultSkipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(1).ElementAtOrDefault(0) - should optimize to x.ElementAtOrDefault(1) (not FirstOrDefault because index after Skip is 1)
		var a = x.Skip(1).ElementAtOrDefault(0);

		// Skip with constant index - should optimize to x.ElementAtOrDefault(2 + 1) => x.ElementAtOrDefault(3)
		var b = x.Skip(2).ElementAtOrDefault(1);

		// Skip followed by AsEnumerable - should still optimize
		var c = x.Skip(1).AsEnumerable().ElementAtOrDefault(2);

		// Skip followed by ToArray, ElementAtOrDefault(0) - should optimize to x.ElementAtOrDefault(1)
		var d = x.Skip(1).ToArray().ElementAtOrDefault(0);

		// Multiple operations that don't affect indexing, then Skip
		var e = x.AsEnumerable().ToList().Skip(1).ElementAtOrDefault(1);

		// Skip with out of bounds index - should return default
		var f = x.Skip(1).ElementAtOrDefault(10);

		// Direct ElementAtOrDefault(0) without Skip - should become FirstOrDefault()
		var g = x.ElementAtOrDefault(0);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.ElementAtOrDefault(1);
			var b = x.ElementAtOrDefault(3);
			var c = x.ElementAtOrDefault(3);
			var d = x.ElementAtOrDefault(1);
			var e = x.ElementAtOrDefault(2);
			var f = x.ElementAtOrDefault(11);
			var g = x.FirstOrDefault();
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 16;", new[] { 1, 2, 3, 4, 5 }), // 2 + 4 + 4 + 2 + 3 + 0 + 1 = 16
		Create("return 0;", new int[] { }), // All return 0 (default)
	];
}

/// <summary>
/// Tests that operations which affect element positions are NOT optimized for ElementAtOrDefault
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultNoOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy should NOT be optimized (changes element positions!)
		var a = x.OrderBy(v => v).ElementAtOrDefault(0);

		// OrderByDescending should NOT be optimized
		var b = x.OrderByDescending(v => v).ElementAtOrDefault(0);

		// Reverse should NOT be optimized
		var c = x.Reverse().ElementAtOrDefault(0);

		// Where should NOT be optimized (changes collection size and indices)
		var d = x.Where(v => v > 2).ElementAtOrDefault(0);

		// Select should NOT be optimized (transforms elements)
		var e = x.Select(v => v * 2).ElementAtOrDefault(0);

		// Distinct should NOT be optimized (removes duplicates, changes indices)
		var f = x.Distinct().ElementAtOrDefault(0);

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Min();
			var b = x.Max();
			var c = x.LastOrDefault();
			var d = x.Where(v => v > 2).FirstOrDefault();
			var e = x.Select(v => v * 2).FirstOrDefault();
			var f = x.FirstOrDefault();
			
			return a + b + c + d + e + f;
			""", Unknown),
		Create("return 17;", new[] { 1, 2, 3, 4, 5 }), // 1 + 5 + 5 + 3 + 2 + 1 = 17
		Create("return 0;", new int[] { }),
	];
}
