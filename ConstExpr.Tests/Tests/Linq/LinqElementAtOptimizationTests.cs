namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ElementAt() optimization - verify that unnecessary operations before ElementAt() are removed
/// and that ElementAt is optimized to direct array/list indexing when possible
/// Note: ElementAt(0) is optimized to First() which is more idiomatic
/// </summary>
[InheritsTests]
public class LinqElementAtOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAt(0) - should become First()
		var a = x.ElementAt(0);

		// AsEnumerable().ElementAt() => ElementAt() => array indexing
		var b = x.AsEnumerable().ElementAt(1);

		// ToList().ElementAt() => ElementAt() => array indexing
		var c = x.ToList().ElementAt(2);

		// ToArray().ElementAt(0) => First()
		var d = x.ToArray().ElementAt(0);

		// AsEnumerable().ToList().ElementAt() => ElementAt() => array indexing
		var e = x.AsEnumerable().ToList().ElementAt(1);

		// Complex chain: AsEnumerable().ToArray().ElementAt() => array indexing
		var f = x.AsEnumerable().ToArray().ElementAt(2);

		// ElementAt with different index
		var g = x.ElementAt(3);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x[0];
			var b = x[1];
			var c = x[2];
			var d = x[0];
			var e = x[1];
			var f = x[2];
			var g = x[3];
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 16;", new[] { 1, 2, 3, 4, 5 }), // 1 + 2 + 3 + 1 + 2 + 3 + 4 = 16
		Create("return 0;", new[] { 0, 0, 0, 0, 0 }),
	];
}

/// <summary>
/// Tests for ElementAt() optimization on List - verify that ElementAt is optimized to list indexing
/// ElementAt(0) becomes First()
/// </summary>
[InheritsTests]
public class LinqElementAtOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAt(0) on List - should become First()
		var a = x.ElementAt(0);

		// AsEnumerable().ElementAt() => list indexing
		var b = x.AsEnumerable().ElementAt(1);

		// ToArray().ElementAt(0) => First()
		var c = x.ToArray().ElementAt(0);

		// ToList().ElementAt() => list indexing
		var d = x.ToList().ElementAt(1);

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x[0];
			var b = x[1];
			var c = x[0];
			var d = x[1];
			
			return a + b + c + d;
			""", Unknown),
		Create("return 6;", new List<int> { 1, 2, 3, 4, 5 }), // 1 + 2 + 1 + 2 = 6
		Create("return 0;", new List<int> { 0, 0, 0, 0, 0 }),
	];
}

/// <summary>
/// Tests for ElementAt() optimization with Skip - verify that Skip is properly optimized
/// When Skip results in ElementAt(0), it should NOT further optimize to First() for arrays because
/// we already have direct indexing. Direct ElementAt(0) without Skip DOES become First().
/// </summary>
[InheritsTests]
public class LinqElementAtSkipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Skip(1).ElementAt(0) - should optimize to x[1] (not First because we have array access)
		var a = x.Skip(1).ElementAt(0);

		// Skip with constant index - should optimize to x[2 + 1] => x[3]
		var b = x.Skip(2).ElementAt(1);

		// Skip followed by AsEnumerable - should still optimize
		var c = x.Skip(1).AsEnumerable().ElementAt(2);

		// Skip(1).ToArray().ElementAt(0) - should optimize to x[1]
		var d = x.Skip(1).ToArray().ElementAt(0);

		// Multiple operations that don't affect indexing, then Skip
		var e = x.AsEnumerable().ToList().Skip(1).ElementAt(1);

		// Direct ElementAt(0) without Skip - should become First()
		var f = x.ElementAt(0);

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x[1];
			var b = x[3];
			var c = x[3];
			var d = x[1];
			var e = x[2];
			var f = x[0];
			
			return a + b + c + d + e + f;
			""", Unknown),
		// a = x[1] = 2, b = x[3] = 4, c = x[3] = 4, d = x[1] = 2, e = x[2] = 3, f = x[0] = 1
		// Total: 2 + 4 + 4 + 2 + 3 + 1 = 16
		Create("return 16;", new[] { 1, 2, 3, 4, 5 }),
		Create("throw new ArgumentOutOfRangeException(\"Specified argument was out of the range of valid values. (Parameter 'index')\");", new int[] { }),
	];
}

/// <summary>
/// Tests that operations which affect element positions are NOT optimized
/// </summary>
[InheritsTests]
public class LinqElementAtNoOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy should  be optimized (changes element positions!)
		var a = x.OrderBy(v => v).ElementAt(0);

		// OrderByDescending should  be optimized
		var b = x.OrderByDescending(v => v).ElementAt(0);

		// Reverse should  be optimized
		var c = x.Reverse().ElementAt(0);

		// Where should  be optimized (changes collection size and indices)
		var d = x.Where(v => v > 2).ElementAt(0);

		// Select should  be optimized (transforms elements)
		var e = x.Select(v => v * 2).ElementAt(0);

		// Distinct should  be optimized (removes duplicates, changes indices)
		var f = x.Distinct().ElementAt(0);
		
		// Take should be optimized (limits collection)
		var g = x.Take(3).ElementAt(0);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Min();
			var b = x.Max();
			var c = x[^1];
			var d = x.First(v => v > 2);
			var e = x.Select(v => v << 1).First();
			var f = x[0];
			var g = x[0];
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 18;", new[] { 1, 2, 3, 4, 5 }), // 1 + 5 + 5 + 3 + 2 + 1 + 1 = 18
		Create("throw new ArgumentOutOfRangeException(\"Specified argument was out of the range of valid values. (Parameter 'index')\");", new int[] { }),
	];
}
