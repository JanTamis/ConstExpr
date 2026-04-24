namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToArray() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToArrayOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToArray().ToArray() => ToArray()
		var a = x.ToArray().ToArray().Length;

		// ToList().ToArray() => ToArray()
		var b = x.ToList().ToArray().Length;

		// AsEnumerable().ToList().ToArray() => ToArray()
		var c = x.AsEnumerable().ToList().ToArray().Length;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = x.Length;
			var b = x.Length;
			var c = x.Length;
			
			return a + b + c;
			"""),
		Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

/// <summary>
/// Tests for arr.Where(p).ToArray() => Array.FindAll(arr, p)
/// </summary>
[InheritsTests]
public class LinqWhereToArrayOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// arr.Where(p).ToArray() => Array.FindAll(arr, p)
		var a = x.Where(v => v > 2).ToArray().Length;

		// Redundant materializing before Where should be stripped transparently
		var b = x.ToList().Where(v => v > 2).ToArray().Length;

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Array.FindAll(x, v => v > 2).Length;
			var b = Array.FindAll(x, v => v > 2).Length;
			
			return a + b;
			"""),
		Create("return 6;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}

/// <summary>
/// Tests for arr.Select(f).Where(p).ToArray() => Array.FindAll(arr, x => p(f(x)))
/// </summary>
[InheritsTests]
public class LinqSelectWhereToArrayOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// arr.Select(f).Where(p).ToArray() => Array.FindAll(arr, x => p(f(x)))
		var a = x.Select(v => v * 2).Where(v => v > 4).ToArray().Length;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Array.FindAll(x, v => v << 1 > 4).Length;
			
			return a;
			"""),
		Create("return 3;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}
